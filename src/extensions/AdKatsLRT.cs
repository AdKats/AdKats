/* 
 * AdKatsLRT - On-Spawn Loadout Enforcer
 * 
 * AdKats and respective extensions are inspired by the gaming community A Different Kind (ADK). 
 * Visit http://www.ADKGamers.com/ for more information.
 *
 * The AdKats Frostbite Plugin is open source, and under public domain, but certain extensions are not. 
 * The AdKatsLRT extension is not open for free distribution, copyright Daniel J. Gradinjan, with all rights reserved.
 * 
 * Development by Daniel J. Gradinjan (ColColonCleaner)
 * 
 * AdKatsLRT.cs
 * Version 3.0.0.0
 * 28-MAR-2026
 *
 * Automatic Update Information
 * <version_code>3.0.0.0</version_code>
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

using Flurl;
using Flurl.Http;

using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public class AdKatsLRT : PRoConPluginAPI, IPRoConPluginInterface
    {
        //Current Plugin Version
        private const String PluginVersion = "3.0.0.0";

        public readonly Logger Log;

        public enum GameVersion
        {
            BF3,
            BF4,
            BFHL
        };

        //Constants
        private const String SettingsInstancePrefix = "0. Instance Settings|";
        private const String SettingsDisplayPrefix = "1. Display Settings|";
        private const String SettingsPresetPrefix = "2. Preset Settings|";
        private const String SettingsMapModePrefix = "3. Map/Mode Settings";
        private const String SettingsWeaponPrefix = "4. Weapons - ";
        private const String SettingsAccessoryPrefix = "5. Weapon Accessories - ";
        private const String SettingsGadgetPrefix = "6. Gadgets - ";
        private const String SettingsVehiclePrefix = "7. Vehicle Weapons/Unlocks";
        private const String SettingsDeniedItemMessagePrefix = "8A. Denied Item Kill Messages|";
        private const String SettingsDeniedItemAccMessagePrefix = "8B. Denied Item Accessory Kill Messages|";
        private const String SettingsDeniedVehicleItemMessagePrefix = "8C. Denied Vehicle Item Kill Messages|";

        //State
        private GameVersion _gameVersion = GameVersion.BF3;
        private volatile Boolean _pluginEnabled;
        private DateTime _pluginStartTime = DateTime.UtcNow;
        private WarsawLibrary _warsawLibrary = new WarsawLibrary();
        private Boolean _warsawLibraryLoaded;
        private readonly HashSet<String> _adminList = new HashSet<string>();
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _playerDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private Boolean _firstPlayerListComplete;
        private Boolean _isTestingAuthorized;
        private readonly Dictionary<String, AdKatsSubscribedPlayer> _playerLeftDictionary = new Dictionary<String, AdKatsSubscribedPlayer>();
        private readonly Queue<ProcessObject> _loadoutProcessingQueue = new Queue<ProcessObject>();
        private readonly Queue<AdKatsSubscribedPlayer> _battlelogFetchQueue = new Queue<AdKatsSubscribedPlayer>();
        private readonly Dictionary<String, String> _warsawInvalidLoadoutIDMessages = new Dictionary<String, String>();
        private readonly Dictionary<String, String> _warsawInvalidVehicleLoadoutIDMessages = new Dictionary<String, String>();
        private readonly HashSet<String> _warsawSpawnDeniedIDs = new HashSet<String>();
        private Int32 _countKilled;
        private Int32 _countFixed;
        private Int32 _countQuit;
        private readonly AdKatsServer _serverInfo;
        private Boolean _displayLoadoutDebug;

        //Settings
        private Boolean _highRequestVolume;
        private Boolean _useProxy = false;
        private String _proxyURL = "";
        private Boolean _enableAdKatsIntegration;
        private Boolean _spawnEnforcementOnly;
        private Boolean _spawnEnforcementActOnAdmins;
        private Boolean _spawnEnforcementActOnReputablePlayers;
        private Boolean _displayWeaponPopularity;
        private Int32 _weaponPopularityDisplayMinutes = 6;
        private Boolean _useWeaponCatchingBackup = true;
        private Int32 _triggerEnforcementMinimumInfractionPoints = 6;
        private Boolean _spawnEnforceAllVehicles;
        private String[] _Whitelist = { };
        private String[] _ItemFilter = { };

        //Display
        private Boolean _displayMapsModes;
        private Boolean _displayWeapons;
        private Boolean _displayWeaponAccessories;
        private Boolean _displayGadgets;
        private Boolean _displayVehicles;

        //Maps Modes
        private Boolean _restrictSpecificMapModes;
        private List<MapMode> _availableMapModes = new List<MapMode>();
        private readonly Dictionary<String, MapMode> _restrictedMapModes = new Dictionary<String, MapMode>();

        //Timing
        private readonly DateTime _proconStartTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private readonly TimeSpan _battlelogWaitDuration = TimeSpan.FromSeconds(3);
        private DateTime _startTime = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastVersionTrackingUpdate = DateTime.UtcNow - TimeSpan.FromHours(1);
        private Object _battlelogLocker = new Object();
        private DateTime _lastBattlelogAction = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private DateTime _lastBattlelogFrequencyMessage = DateTime.UtcNow - TimeSpan.FromSeconds(5);
        private Queue<DateTime> _BattlelogActionTimes = new Queue<DateTime>();
        private DateTime _lastCategoryListing = DateTime.UtcNow;

        //Threads
        private readonly Dictionary<Int32, Thread> _aliveThreads = new Dictionary<Int32, Thread>();
        private volatile Boolean _threadsReady;
        private EventWaitHandle _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _loadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _playerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private EventWaitHandle _battlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread _activator;
        private Thread _finalizer;
        private Thread _spawnProcessingThread;
        private Thread _battlelogCommThread;

        //AutoAdmin
        private Boolean _UseBackupAutoadmin;
        private Boolean _UseAdKatsPunishments;
        private Dictionary<String, List<String>> _WarsawRCONMappings = new Dictionary<String, List<String>>();
        private Dictionary<String, List<String>> _RCONWarsawMappings = new Dictionary<String, List<String>>();

        //Settings
        private const Int32 YellDuration = 7;

        //Debug
        private Boolean _slowmo;

        public AdKatsLRT()
        {
            Log = new Logger(this);

            //Create the server reference
            _serverInfo = new AdKatsServer(this);

            //Populate maps/modes
            PopulateMapModes();

            //Populate AutoAdmin Weapon Mappings
            PopulateWarsawRCONCodes();

            //Set defaults for webclient
            ServicePointManager.Expect100Continue = false;

            //By default plugin is not enabled or ready
            _pluginEnabled = false;
            _threadsReady = false;

            //Debug level is 0 by default
            Log.DebugLevel = 0;

            //Prepare the status monitor
            SetupStatusMonitor();
        }

        public String GetPluginName()
        {
            return "AdKatsLRT - Loadout Enforcer";
        }

        public String GetPluginVersion()
        {
            return PluginVersion;
        }

        public String GetPluginAuthor()
        {
            return "[ADK]ColColonCleaner";
        }

        public String GetPluginWebsite()
        {
            return "https://github.com/AdKats/";
        }

        public String GetPluginDescription()
        {
            return "";
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            try
            {
                const string separator = " | ";

                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Enable High Request Volume", typeof(Boolean), _highRequestVolume));
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Proxy for Battlelog", typeof(Boolean), _useProxy));
                if (_useProxy)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Proxy URL", typeof(String), _proxyURL));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
                if (_enableAdKatsIntegration)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforcement Only", typeof(Boolean), _spawnEnforcementOnly));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Action Whitelist", typeof(String[]), _Whitelist));
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Display Weapon Popularity Periodically", typeof(Boolean), _displayWeaponPopularity));
                if (_displayWeaponPopularity)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Weapon Popularity Display Frequency Minutes", typeof(Int32), _weaponPopularityDisplayMinutes));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Backup AutoAdmin", typeof(Boolean), _UseBackupAutoadmin));
                if (_enableAdKatsIntegration && _UseBackupAutoadmin)
                {
                    lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Backup AutoAdmin Use AdKats Punishments", typeof(Boolean), _UseAdKatsPunishments));
                }
                lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Global Item Filter", typeof(String[]), _ItemFilter));
                if (!_warsawLibraryLoaded)
                {
                    lstReturn.Add(new CPluginVariable("The WARSAW library must be loaded to view settings.", typeof(String), "Enable the plugin to fetch the library."));
                    return lstReturn;
                }

                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
                lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));

                if (_displayMapsModes)
                {
                    lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + separator.Trim() + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
                    if (_restrictSpecificMapModes)
                    {
                        lstReturn.AddRange(_availableMapModes.OrderBy(mm => mm.ModeName).ThenBy(mm => mm.MapName).Select(mapMode => new CPluginVariable(SettingsMapModePrefix + " - " + mapMode.ModeName + separator.Trim() + "RMM" + mapMode.MapModeID.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') + separator + mapMode.MapName + separator + "Enforce?", "enum.EnforceMapEnum(Enforce|Ignore)", _restrictedMapModes.ContainsKey(mapMode.ModeKey + "|" + mapMode.MapKey) ? ("Enforce") : ("Ignore"))));
                    }
                }

                //Run removals
                _warsawSpawnDeniedIDs.RemoveWhere(spawnID => !_warsawInvalidLoadoutIDMessages.ContainsKey(spawnID) && !_warsawInvalidVehicleLoadoutIDMessages.ContainsKey(spawnID));

                if (_displayWeapons)
                {
                    if (_warsawLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _warsawLibrary.Items.Values.Where(weapon => weapon.CategoryReadable != "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weapon.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsWeaponPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayWeaponAccessories)
                {
                    if (_warsawLibrary.ItemAccessories.Any())
                    {
                        foreach (WarsawItemAccessory weaponAccessory in _warsawLibrary.ItemAccessories.Values.OrderBy(weaponAccessory => weaponAccessory.Slug).ThenBy(weaponAccessory => weaponAccessory.CategoryReadable))
                        {
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weaponAccessory.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWS" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsAccessoryPrefix + weaponAccessory.CategoryReadable + "|ALWT" + weaponAccessory.WarsawID + separator + weaponAccessory.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayGadgets)
                {
                    if (_warsawLibrary.Items.Any())
                    {
                        foreach (WarsawItem weapon in _warsawLibrary.Items.Values.Where(weapon => weapon.CategoryReadable == "GADGET").OrderBy(weapon => weapon.CategoryReadable).ThenBy(weapon => weapon.Slug))
                        {
                            if (String.IsNullOrEmpty(weapon.CategoryTypeReadable))
                            {
                                Log.Error(weapon.WarsawID + " did not have a category type.");
                            }
                            if (_ItemFilter.Any() && !_ItemFilter.Any(item => weapon.Slug.ToLower().Contains(item.ToLower())))
                            {
                                continue;
                            }
                            if (_enableAdKatsIntegration && !_spawnEnforcementOnly)
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on trigger?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(weapon.WarsawID))
                                {
                                    lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWS" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                                }
                            }
                            else
                            {
                                lstReturn.Add(new CPluginVariable(SettingsGadgetPrefix + weapon.CategoryTypeReadable + "|ALWT" + weapon.WarsawID + separator + weapon.Slug + separator + "Allow on spawn?", "enum.AllowItemEnum(Allow|Deny)", _warsawSpawnDeniedIDs.Contains(weapon.WarsawID) ? ("Deny") : ("Allow")));
                            }
                        }
                    }
                }
                if (_displayVehicles)
                {
                    lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
                    if (_warsawLibrary.Vehicles.Any())
                    {
                        foreach (var vehicle in _warsawLibrary.Vehicles.Values.OrderBy(vec => vec.CategoryType))
                        {
                            String currentPrefix = SettingsVehiclePrefix + " - " + vehicle.CategoryType + "|";
                            lstReturn.AddRange(vehicle.AllowedPrimaries.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedSecondaries.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedCountermeasures.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedOptics.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedUpgrades.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedSecondariesGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedOpticsGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                            lstReturn.AddRange(vehicle.AllowedUpgradesGunner.Values.Where(unlock => !_ItemFilter.Any() || _ItemFilter.Any(item => unlock.Slug.ToLower().Contains(item.ToLower()))).Select(unlock => new CPluginVariable(currentPrefix + "ALWK" + unlock.WarsawID + separator + unlock.Slug + separator + "Allow on " + ((_spawnEnforceAllVehicles) ? ("spawn") : ("kill")) + "?", "enum.AllowItemEnum(Allow|Deny)", _warsawInvalidVehicleLoadoutIDMessages.ContainsKey(unlock.WarsawID) ? ("Deny") : ("Allow"))));
                        }
                    }
                }
                foreach (var pair in _warsawInvalidLoadoutIDMessages.Where(denied => _warsawLibrary.Items.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedItem;
                    if (_warsawLibrary.Items.TryGetValue(pair.Key, out deniedItem))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemMessagePrefix + "MSG" + deniedItem.WarsawID + separator + deniedItem.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _warsawInvalidLoadoutIDMessages.Where(denied => _warsawLibrary.ItemAccessories.ContainsKey(denied.Key)))
                {
                    WarsawItemAccessory deniedItemAccessory;
                    if (_warsawLibrary.ItemAccessories.TryGetValue(pair.Key, out deniedItemAccessory))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedItemAccMessagePrefix + "MSG" + deniedItemAccessory.WarsawID + separator + deniedItemAccessory.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                foreach (var pair in _warsawInvalidVehicleLoadoutIDMessages.Where(denied => _warsawLibrary.VehicleUnlocks.ContainsKey(denied.Key)))
                {
                    WarsawItem deniedVehicleUnlock;
                    if (_warsawLibrary.VehicleUnlocks.TryGetValue(pair.Key, out deniedVehicleUnlock))
                    {
                        lstReturn.Add(new CPluginVariable(SettingsDeniedVehicleItemMessagePrefix + "VMSG" + deniedVehicleUnlock.WarsawID + separator + deniedVehicleUnlock.Slug + separator + "Kill Message", typeof(String), pair.Value));
                    }
                }
                lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), Log.DebugLevel));
            }
            catch (Exception e)
            {
                Log.Exception("Error while getting display plugin variables", e);
            }
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            var lstReturn = new List<CPluginVariable>();
            const string separator = " | ";

            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Enable High Request Volume", typeof(Boolean), _highRequestVolume));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Proxy for Battlelog", typeof(Boolean), _useProxy));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Proxy URL", typeof(String), _proxyURL));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Integrate with AdKats", typeof(Boolean), _enableAdKatsIntegration));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Use Backup AutoAdmin", typeof(Boolean), _UseBackupAutoadmin));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforcement Only", typeof(Boolean), _spawnEnforcementOnly));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Backup AutoAdmin Use AdKats Punishments", typeof(Boolean), _UseAdKatsPunishments));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Admins", typeof(Boolean), _spawnEnforcementActOnAdmins));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Spawn Enforce Reputable Players", typeof(Boolean), _spawnEnforcementActOnReputablePlayers));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Display Weapon Popularity Periodically", typeof(Boolean), _displayWeaponPopularity));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Action Whitelist", typeof(String[]), _Whitelist));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Trigger Enforce Minimum Infraction Points", typeof(Int32), _triggerEnforcementMinimumInfractionPoints));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Weapon Popularity Display Frequency Minutes", typeof(Int32), _weaponPopularityDisplayMinutes));
            lstReturn.Add(new CPluginVariable(SettingsInstancePrefix + "Global Item Filter", typeof(String[]), _ItemFilter));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Map/Mode Settings", typeof(Boolean), _displayMapsModes));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Settings", typeof(Boolean), _displayWeapons));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Weapon Accessory Settings", typeof(Boolean), _displayWeaponAccessories));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Gadget Settings", typeof(Boolean), _displayGadgets));
            lstReturn.Add(new CPluginVariable(SettingsDisplayPrefix + "Display Vehicle Settings", typeof(Boolean), _displayVehicles));
            lstReturn.Add(new CPluginVariable(SettingsMapModePrefix + "Enforce on Specific Maps/Modes Only", typeof(Boolean), _restrictSpecificMapModes));
            lstReturn.Add(new CPluginVariable(SettingsVehiclePrefix + separator.Trim() + "Spawn Enforce all Vehicles", typeof(Boolean), _spawnEnforceAllVehicles));
            lstReturn.AddRange(_warsawInvalidLoadoutIDMessages.Select(pair => new CPluginVariable("MSG" + pair.Key, typeof(String), pair.Value)));
            lstReturn.AddRange(_warsawInvalidVehicleLoadoutIDMessages.Select(pair => new CPluginVariable("VMSG" + pair.Key, typeof(String), pair.Value)));
            _warsawSpawnDeniedIDs.RemoveWhere(spawnID => !_warsawInvalidLoadoutIDMessages.ContainsKey(spawnID));
            lstReturn.AddRange(_warsawSpawnDeniedIDs.Select(deniedSpawnID => new CPluginVariable("ALWS" + deniedSpawnID, typeof(String), "Deny")));
            lstReturn.AddRange(_restrictedMapModes.Values.Select(restrictedMapMode => new CPluginVariable("RMM" + restrictedMapMode.MapModeID, typeof(String), "Enforce")));
            lstReturn.Add(new CPluginVariable("D99. Debugging|Debug level", typeof(int), Log.DebugLevel));
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            if (strValue == null)
            {
                return;
            }
            try
            {
                if (strVariable == "UpdateSettings")
                {
                    //Settings page will be updated after return.
                }
                else if (Regex.Match(strVariable, @"Debug level").Success)
                {
                    Int32 tmp;
                    if (int.TryParse(strValue, out tmp))
                    {
                        if (tmp == 269)
                        {
                            Log.Success("Extended Debug Mode Toggled");
                            _displayLoadoutDebug = !_displayLoadoutDebug;
                            return;
                        }
                        else if (tmp == 2232)
                        {
                            Environment.Exit(2232);
                        }
                        Log.DebugLevel = tmp;
                    }
                }
                else if (Regex.Match(strVariable, @"Enable High Request Volume").Success)
                {
                    Boolean highRequestVolume = Boolean.Parse(strValue);
                    if (highRequestVolume != _highRequestVolume)
                    {
                        _highRequestVolume = highRequestVolume;
                    }
                }
                else if (Regex.Match(strVariable, @"Use Proxy for Battlelog").Success)
                {
                    Boolean useProxy = Boolean.Parse(strValue);
                    if (useProxy != _useProxy)
                    {
                        _useProxy = useProxy;
                    }
                }
                else if (Regex.Match(strVariable, @"Proxy URL").Success)
                {
                    try
                    {
                        if (!String.IsNullOrEmpty(strValue))
                        {
                            Uri uri = new Uri(strValue);
                            Log.Debug("Proxy URL set to " + strValue + ".", 1);
                        }
                    }
                    catch (UriFormatException)
                    {
                        strValue = _proxyURL;
                        Log.Warn("Invalid Proxy URL! Make sure that the URI is valid!");
                    }
                    if (!_proxyURL.Equals(strValue))
                    {
                        _proxyURL = strValue;
                    }
                }
                else if (Regex.Match(strVariable, @"Integrate with AdKats").Success)
                {
                    Boolean enableAdKatsIntegration = Boolean.Parse(strValue);
                    if (enableAdKatsIntegration != _enableAdKatsIntegration)
                    {
                        if (!enableAdKatsIntegration)
                        {
                            _UseAdKatsPunishments = false;
                        }
                        if (_threadsReady)
                        {
                            Log.Info("AdKatsLRT must be rebooted to modify this setting.");
                            Disable();
                        }
                        _enableAdKatsIntegration = enableAdKatsIntegration;
                    }
                }
                else if (Regex.Match(strVariable, @"Display Map/Mode Settings").Success)
                {
                    Boolean displayMapsModes = Boolean.Parse(strValue);
                    if (displayMapsModes != _displayMapsModes)
                    {
                        _displayMapsModes = displayMapsModes;
                        if (_displayMapsModes)
                        {
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Weapon Settings").Success)
                {
                    Boolean displayWeapons = Boolean.Parse(strValue);
                    if (displayWeapons != _displayWeapons)
                    {
                        _displayWeapons = displayWeapons;
                        if (_displayWeapons)
                        {
                            _displayMapsModes = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Weapon Accessory Settings").Success)
                {
                    Boolean displayWeaponAccessories = Boolean.Parse(strValue);
                    if (displayWeaponAccessories != _displayWeaponAccessories)
                    {
                        _displayWeaponAccessories = displayWeaponAccessories;
                        if (_displayWeaponAccessories)
                        {
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayGadgets = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Gadget Settings").Success)
                {
                    Boolean displayGadgets = Boolean.Parse(strValue);
                    if (displayGadgets != _displayGadgets)
                    {
                        _displayGadgets = displayGadgets;
                        if (_displayGadgets)
                        {
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayVehicles = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Display Vehicle Settings").Success)
                {
                    Boolean displayVehicles = Boolean.Parse(strValue);
                    if (displayVehicles != _displayVehicles)
                    {
                        _displayVehicles = displayVehicles;
                        if (_displayVehicles)
                        {
                            _displayMapsModes = false;
                            _displayWeapons = false;
                            _displayWeaponAccessories = false;
                            _displayGadgets = false;
                        }
                    }
                }
                else if (Regex.Match(strVariable, @"Spawn Enforcement Only").Success)
                {
                    _spawnEnforcementOnly = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Use Backup AutoAdmin").Success)
                {
                    _UseBackupAutoadmin = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Backup AutoAdmin Use AdKats Punishments").Success)
                {
                    _UseAdKatsPunishments = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Admins").Success)
                {
                    _spawnEnforcementActOnAdmins = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce Reputable Players").Success)
                {
                    _spawnEnforcementActOnReputablePlayers = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Display Weapon Popularity Periodically").Success)
                {
                    _displayWeaponPopularity = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Spawn Enforce all Vehicles").Success)
                {
                    _spawnEnforceAllVehicles = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Enforce on Specific Maps/Modes Only").Success)
                {
                    _restrictSpecificMapModes = Boolean.Parse(strValue);
                }
                else if (Regex.Match(strVariable, @"Trigger Enforce Minimum Infraction Points").Success)
                {
                    Int32 triggerEnforcementMinimumInfractionPoints;
                    if (int.TryParse(strValue, out triggerEnforcementMinimumInfractionPoints))
                    {
                        if (triggerEnforcementMinimumInfractionPoints < 1)
                        {
                            Log.Error("Minimum infraction points for trigger level enforcement cannot be less than 1, use spawn enforcement instead.");
                            triggerEnforcementMinimumInfractionPoints = 1;
                        }
                        _triggerEnforcementMinimumInfractionPoints = triggerEnforcementMinimumInfractionPoints;
                    }
                }
                else if (Regex.Match(strVariable, @"Weapon Popularity Display Frequency Minutes").Success)
                {
                    Int32 weaponPopularityDisplayMinutes;
                    if (int.TryParse(strValue, out weaponPopularityDisplayMinutes))
                    {
                        if (weaponPopularityDisplayMinutes < 2)
                        {
                            Log.Error("Frequency cannot be less than every 2 minutes.");
                            weaponPopularityDisplayMinutes = 2;
                        }
                        _weaponPopularityDisplayMinutes = weaponPopularityDisplayMinutes;
                    }
                }
                else if (Regex.Match(strVariable, @"Action Whitelist").Success)
                {
                    _Whitelist = CPluginVariable.DecodeStringArray(strValue).Where(entry => !String.IsNullOrEmpty(entry)).ToArray();
                }
                else if (Regex.Match(strVariable, @"Global Item Search Blacklist").Success || Regex.Match(strVariable, @"Global Item Filter").Success)
                {
                    _ItemFilter = CPluginVariable.DecodeStringArray(strValue).Where(entry => !String.IsNullOrEmpty(entry)).ToArray();
                }
                else if (strVariable.StartsWith("ALWT"))
                {
                    //Trim off all but the warsaw ID
                    //ALWT3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWT".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _warsawInvalidLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            _warsawInvalidLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your loadout";
                            if (!_enableAdKatsIntegration || _spawnEnforcementOnly)
                            {
                                if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                                {
                                    _warsawSpawnDeniedIDs.Add(warsawID);
                                }
                            }
                            break;
                    }
                }
                else if (strVariable.StartsWith("ALWK"))
                {
                    //Trim off all but the warsaw ID
                    //ALWK3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWK".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _warsawInvalidVehicleLoadoutIDMessages.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            WarsawItem item;
                            if (!_warsawLibrary.VehicleUnlocks.TryGetValue(warsawID, out item))
                            {
                                Log.Error("Unable to find vehicle unlock " + warsawID);
                                return;
                            }
                            if (item.AssignedVehicle == null)
                            {
                                Log.Error("Unlock item " + warsawID + " was not assigned to a vehicle.");
                                return;
                            }
                            _warsawInvalidVehicleLoadoutIDMessages[warsawID] = "Please remove " + commandSplit[commandSplit.Count() - 2].Trim() + " from your " + item.AssignedVehicle.CategoryType;
                            if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                            {
                                _warsawSpawnDeniedIDs.Add(warsawID);
                            }
                            foreach (var aPlayer in _playerDictionary.Values)
                            {
                                aPlayer.WatchedVehicles.Clear();
                            }
                            break;
                    }
                }
                else if (strVariable.StartsWith("ALWS"))
                {
                    //Trim off all but the warsaw ID
                    //ALWS3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("ALWS".ToCharArray()).Trim();
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "allow":
                            //parse allow
                            _warsawSpawnDeniedIDs.Remove(warsawID);
                            break;
                        case "deny":
                            //parse deny
                            if (!_warsawSpawnDeniedIDs.Contains(warsawID))
                            {
                                _warsawSpawnDeniedIDs.Add(warsawID);
                            }
                            break;
                    }
                }
                else if (strVariable.StartsWith("RMM"))
                {
                    //Trim off all but the warsaw ID
                    //ALWS3495820391
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    Int32 mapModeID = Int32.Parse(commandSplit[0].TrimStart("RMM".ToCharArray()).Trim());
                    MapMode mapMode = _availableMapModes.FirstOrDefault(mm => mm.MapModeID == mapModeID);
                    if (mapMode == null)
                    {
                        Log.Error("Invalid map/mode ID when parsing map enforce settings.");
                        return;
                    }
                    //Fetch needed role
                    switch (strValue.ToLower())
                    {
                        case "enforce":
                            //parse deny
                            if (!_restrictedMapModes.ContainsKey(mapMode.ModeKey + "|" + mapMode.MapKey))
                            {
                                _restrictedMapModes[mapMode.ModeKey + "|" + mapMode.MapKey] = mapMode;
                                if (_warsawLibraryLoaded)
                                {
                                    Log.Info("Enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                                }
                            }
                            break;
                        case "ignore":
                            //parse allow
                            if (_restrictedMapModes.Remove(mapMode.ModeKey + "|" + mapMode.MapKey) && _warsawLibraryLoaded)
                            {
                                Log.Info("No longer enforcing loadout on " + mapMode.ModeName + " " + mapMode.MapName);
                            }
                            break;
                        default:
                            Log.Error("Unknown setting when parsing map enforce settings.");
                            return;
                    }
                }
                else if (strVariable.StartsWith("MSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        Log.Error("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("MSG".ToCharArray()).Trim();
                    _warsawInvalidLoadoutIDMessages[warsawID] = strValue;
                }
                else if (strVariable.StartsWith("VMSG"))
                {
                    //Trim off all but the warsaw ID
                    //MSG3495820391
                    if (String.IsNullOrEmpty(strValue))
                    {
                        Log.Error("Kill messages cannot be empty.");
                        return;
                    }
                    String[] commandSplit = CPluginVariable.DecodeStringArray(strVariable);
                    String warsawID = commandSplit[0].TrimStart("VMSG".ToCharArray()).Trim();
                    _warsawInvalidVehicleLoadoutIDMessages[warsawID] = strValue;
                }
                else
                {
                    Log.Info(strVariable + " =+= " + strValue);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while updating AdKatsLRT settings.", e);
            }
        }

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            Log.Debug("Entering OnPluginLoaded", 7);
            try
            {
                //Set the server IP
                _serverInfo.ServerIP = strHostName + ":" + strPort;
                //Register all events
                RegisterEvents(GetType().Name,
                    "OnVersion",
                    "OnServerInfo",
                    "OnListPlayers",
                    "OnPlayerSpawned",
                    "OnPlayerKilled",
                    "OnPlayerLeft");
            }
            catch (Exception e)
            {
                Log.Exception("FATAL ERROR on plugin load.", e);
            }
            Log.Debug("Exiting OnPluginLoaded", 7);
        }

        public void OnPluginEnable()
        {
            try
            {
                //If the finalizer is still alive, inform the user and disable
                if (_finalizer != null && _finalizer.IsAlive)
                {
                    Log.Error("Cannot enable the plugin while it is shutting down. Please Wait for it to shut down.");
                    _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
                    //Disable the plugin
                    Disable();
                    return;
                }
                if (_gameVersion != GameVersion.BF4)
                {
                    Log.Error("LRT can only be enabled on BF4 at this time.");
                    Disable();
                    return;
                }
                //Create a new thread to activate the plugin
                _activator = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Enabler";

                        _pluginEnabled = true;

                        if ((DateTime.UtcNow - _proconStartTime).TotalSeconds <= 20)
                        {
                            Log.Write("Waiting a few seconds for requirements and other plugins to initialize, please wait...");
                            //Wait on all settings to be imported by procon for initial start, and for all other plugins to start and register.
                            for (Int32 index = 20 - (Int32)(DateTime.UtcNow - _proconStartTime).TotalSeconds; index > 0; index--)
                            {
                                Log.Write(index + "...");
                                _threadMasterWaitHandle.WaitOne(1000);
                            }
                        }
                        if (!_pluginEnabled)
                        {
                            LogThreadExit();
                            return;
                        }
                        Boolean adKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                        if (adKatsFound)
                        {
                            _enableAdKatsIntegration = true;
                        }
                        if (!_enableAdKatsIntegration || adKatsFound)
                        {
                            _startTime = DateTime.UtcNow;
                            //Set the enabled variable
                            _playerProcessingWaitHandle.Reset();

                            if (!_pluginEnabled)
                            {
                                LogThreadExit();
                                return;
                            }
                            //Fetch all weapon names
                            if (_warsawLibraryLoaded || LoadWarsawLibrary())
                            {
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }
                                Log.Success("WARSAW library loaded. " + _warsawLibrary.Items.Count + " items, " + _warsawLibrary.VehicleUnlocks.Count + " vehicle unlocks, and " + _warsawLibrary.ItemAccessories.Count + " accessories.");
                                UpdateSettingPage();

                                if (_enableAdKatsIntegration)
                                {
                                    //Subscribe to online soldiers from AdKats
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"subscription_group", "OnlineSoldiers"},
                                        {"subscription_method", "ReceiveOnlineSoldiers"},
                                        {"subscription_enabled", true}
                                    }));
                                    Log.Info("Waiting for player listing response from AdKats.");
                                }
                                else
                                {
                                    Log.Info("Waiting for first player list event.");
                                }
                                _playerProcessingWaitHandle.WaitOne(Timeout.Infinite);
                                if (!_pluginEnabled)
                                {
                                    LogThreadExit();
                                    return;
                                }

                                _pluginStartTime = DateTime.UtcNow;

                                //Init and start all the threads
                                InitWaitHandles();
                                OpenAllHandles();
                                InitThreads();
                                StartThreads();

                                Log.Success("AdKatsLRT " + GetPluginVersion() + " startup complete [" + FormatTimeString(DateTime.UtcNow - _startTime, 3) + "]. Loadout restriction now online.");
                            }
                            else
                            {
                                Log.Error("Failed to load WARSAW library. AdKatsLRT cannot be started.");
                                Disable();
                            }
                        }
                        else
                        {
                            Log.Error("AdKats not installed or enabled. AdKatsLRT cannot be started.");
                            Disable();
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error while enabling AdKatsLRT.", e);
                    }
                    LogThreadExit();
                }));

                Log.Write("^b^2ENABLED!^n^0 Beginning startup sequence...");
                //Start the thread
                StartAndLogThread(_activator);
            }
            catch (Exception e)
            {
                Log.Exception("Error while initializing activator thread.", e);
            }
        }

        public void OnPluginDisable()
        {
            //If the plugin is already disabling then cancel
            if (_finalizer != null && _finalizer.IsAlive)
                return;
            try
            {
                //Create a new thread to disabled the plugin
                _finalizer = new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        Thread.CurrentThread.Name = "Finalizer";
                        Log.Info("Shutting down AdKatsLRT.");
                        //Disable settings
                        _pluginEnabled = false;
                        _threadsReady = false;

                        if (_enableAdKatsIntegration)
                        {
                            //Unsubscribe from online soldiers through AdKats
                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "SubscribeAsClient", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                {"caller_identity", "AdKatsLRT"},
                                {"response_requested", false},
                                {"subscription_group", "OnlineSoldiers"},
                                {"subscription_method", "ReceiveOnlineSoldiers"},
                                {"subscription_enabled", false}
                            }));
                        }

                        //Open all handles. Threads will finish on their own.
                        OpenAllHandles();

                        //Check to make sure all threads have completed and stopped
                        Int32 attempts = 0;
                        Boolean alive = false;
                        do
                        {
                            OpenAllHandles();
                            attempts++;
                            Thread.Sleep(500);
                            alive = false;
                            String aliveThreads = "";
                            lock (_aliveThreads)
                            {
                                foreach (Int32 deadThreadID in _aliveThreads.Values.Where(thread => !thread.IsAlive).Select(thread => thread.ManagedThreadId).ToList())
                                {
                                    _aliveThreads.Remove(deadThreadID);
                                }
                                foreach (Thread aliveThread in _aliveThreads.Values.ToList())
                                {
                                    alive = true;
                                    aliveThreads += (aliveThread.Name + "[" + aliveThread.ManagedThreadId + "] ");
                                }
                            }
                            if (aliveThreads.Length > 0)
                            {
                                if (attempts > 20)
                                {
                                    Log.Warn("Threads still exiting: " + aliveThreads);
                                }
                                else
                                {
                                    Log.Debug("Threads still exiting: " + aliveThreads, 2);
                                }
                            }
                        } while (alive);
                        _firstPlayerListComplete = false;
                        _playerDictionary.Clear();
                        _playerLeftDictionary.Clear();
                        _loadoutProcessingQueue.Clear();
                        _firstPlayerListComplete = false;
                        _countFixed = 0;
                        _countKilled = 0;
                        _countQuit = 0;
                        _slowmo = false;
                        Log.Write("^b^1AdKatsLRT " + GetPluginVersion() + " Disabled! =(^n^0");
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error occured while disabling AdkatsLRT.", e);
                    }
                }));

                //Start the finalizer thread
                _finalizer.Start();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while initializing AdKatsLRT disable thread.", e);
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            Log.Debug("Entering OnServerInfo", 7);
            try
            {
                if (_pluginEnabled)
                {
                    lock (_serverInfo)
                    {
                        if (serverInfo != null)
                        {
                            //Get the server info
                            _serverInfo.SetInfoObject(serverInfo);

                            Boolean hadServerName = !String.IsNullOrEmpty(_serverInfo.ServerName);
                            _serverInfo.ServerName = serverInfo.ServerName;
                            Boolean haveServerName = !String.IsNullOrEmpty(_serverInfo.ServerName);
                            Boolean wasADK = _isTestingAuthorized;
                            _isTestingAuthorized = serverInfo.ServerName.Contains("=ADK=");
                            if (!wasADK && _isTestingAuthorized)
                            {
                                Log.Info("LRT is testing authorized.");
                            }
                        }
                        else
                        {
                            Log.Error("Server info was null");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while processing server info.", e);
            }
            Log.Debug("Exiting OnServerInfo", 7);
        }

        public override void OnPlayerKilled(Kill kill)
        {
            Log.Debug("Entering OnPlayerKilled", 7);
            try
            {
                //If the plugin is not enabled and running just return
                if (!_pluginEnabled || !_threadsReady || !_firstPlayerListComplete)
                {
                    return;
                }
                //Fetch players
                AdKatsSubscribedPlayer killer;
                AdKatsSubscribedPlayer victim;
                if (kill.Killer != null && !String.IsNullOrEmpty(kill.Killer.SoldierName))
                {
                    if (!_playerDictionary.TryGetValue(kill.Killer.SoldierName, out killer))
                    {
                        Log.Error("Unable to fetch killer " + kill.Killer.SoldierName + " on kill.");
                        return;
                    }
                }
                else
                {
                    return;
                }
                if (kill.Victim != null && !String.IsNullOrEmpty(kill.Victim.SoldierName))
                {
                    if (!_playerDictionary.TryGetValue(kill.Victim.SoldierName, out victim))
                    {
                        Log.Error("Unable to fetch victim " + kill.Victim.SoldierName + " on kill.");
                        return;
                    }
                }
                else
                {
                    return;
                }

                WarsawVehicle vehicle;
                //Check for vehicle restrictions
                if (killer.Loadout != null &&
                    killer.Loadout.LoadoutRCONVehicles.TryGetValue(kill.DamageType, out vehicle))
                {
                    Log.Debug(killer.Name + " is using trackable vehicle type " + vehicle.CategoryType + ".", 5);
                    if (!killer.WatchedVehicles.Contains(vehicle.Category))
                    {
                        killer.WatchedVehicles.Add(vehicle.Category);
                        Log.Debug("Loadout check automatically called on " + killer.Name + " for trackable vehicle kill.", 4);
                        QueueForProcessing(new ProcessObject()
                        {
                            ProcessPlayer = killer,
                            ProcessReason = "vehiclekill",
                            ProcessTime = DateTime.UtcNow
                        });
                    }
                }
                else if (_UseBackupAutoadmin &&
                           _serverInfo.InfoObject.GameMode != "GunMaster0" &&
                           _serverInfo.InfoObject.GameMode != "GunMaster1" &&
                           (!_restrictSpecificMapModes || _restrictedMapModes.ContainsKey(_serverInfo.InfoObject.GameMode + "|" + _serverInfo.InfoObject.Map)))
                {
                    String rejectionMessage = null;

                    List<String> matchingKillWarsaw;
                    if (_RCONWarsawMappings.TryGetValue(kill.DamageType, out matchingKillWarsaw))
                    {
                        foreach (String warsawID in matchingKillWarsaw)
                        {
                            if (_warsawInvalidLoadoutIDMessages.ContainsKey(warsawID))
                            {
                                rejectionMessage = _warsawInvalidLoadoutIDMessages[warsawID];
                                break;
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(rejectionMessage))
                    {
                        if (_enableAdKatsIntegration)
                        {
                            String action = "player_kill";
                            if (_UseAdKatsPunishments)
                            {
                                action = "player_punish";
                            }
                            else if (killer.Punished)
                            {
                                action = "player_kick";
                            }
                            else
                            {
                                killer.Punished = true;
                            }
                            var requestHashtable = new Hashtable {
                                {"caller_identity", "AdKatsLRT"},
                                {"response_requested", false},
                                {"command_type", action},
                                {"source_name", "LoadoutEnforcer"},
                                {"target_name", killer.Name},
                                {"target_guid", killer.GUID},
                                {"record_message", rejectionMessage}
                            };
                            Log.Info("Sending backup AutoAdmin " + action + " to AdKats for " + killer.GetVerboseName());
                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "IssueCommand", "AdKatsLRT", JSON.JsonEncode(requestHashtable));
                        }
                        else
                        {
                            //Weapon is invalid, perform kill or kick based on previous actions
                            if (killer.Punished)
                            {
                                Log.Info("Kicking " + killer.GetVerboseName() + " for using restricted item. [" + rejectionMessage + "].");
                                AdminSayMessage(killer.GetVerboseName() + " was KICKED by LoadoutEnforcer for " + rejectionMessage + ".");
                                ExecuteCommand("procon.protected.send", "admin.kickPlayer", killer.Name, GenerateKickReason(rejectionMessage, "LoadoutEnforcer"));
                            }
                            else
                            {
                                killer.Punished = true;
                                PlayerTellMessage(killer.Name, rejectionMessage);
                                AdminSayMessage(killer.GetVerboseName() + " was KILLED by LoadoutEnforcer for " + rejectionMessage + ".");
                                ExecuteCommand("procon.protected.send", "admin.killPlayer", killer.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while handling OnPlayerKilled.", e);
            }
            Log.Debug("Exiting OnPlayerKilled", 7);
        }

        public String GenerateKickReason(String reason, String source)
        {
            String sourceNameString = "[" + source + "]";

            //Create the full message
            String fullMessage = reason + " " + sourceNameString;

            //Trim the kick message if necessary
            Int32 cutLength = fullMessage.Length - 80;
            if (cutLength > 0)
            {
                String cutReason = reason.Substring(0, reason.Length - cutLength);
                fullMessage = cutReason + " " + sourceNameString;
            }
            return fullMessage;
        }

        private void SetupStatusMonitor()
        {
            //Create a new thread to handle status monitoring
            //This thread will remain running for the duration the layer is online
            var statusMonitorThread = new Thread(new ThreadStart(delegate
            {
                try
                {
                    Thread.CurrentThread.Name = "StatusMonitor";
                    DateTime lastKeepAliveCheck = DateTime.UtcNow;
                    DateTime lastAdminFetch = DateTime.UtcNow;
                    while (true)
                    {
                        try
                        {
                            //Check for thread warning every 30 seconds
                            if ((DateTime.UtcNow - lastKeepAliveCheck).TotalSeconds > 30)
                            {
                                if (_threadsReady)
                                {
                                    Boolean adKatsFound = GetRegisteredCommands().Any(command => command.RegisteredClassname == "AdKats" && command.RegisteredMethodName == "PluginEnabled");
                                    if (adKatsFound)
                                    {
                                        if (!_enableAdKatsIntegration)
                                        {
                                            Log.Error("AdKats found, but integration not enabled, disabling.");
                                            Disable();
                                        }
                                    }
                                    else if (_enableAdKatsIntegration)
                                    {
                                        Log.Error("AdKats was disabled. AdKatsLRT has integration enabled, and must shut down if that plugin shuts down.");
                                        Disable();
                                    }
                                }
                                lastKeepAliveCheck = DateTime.UtcNow;

                                lock (_aliveThreads)
                                {
                                    foreach (Int32 deadThreadID in _aliveThreads.Values.Where(thread => !thread.IsAlive).Select(thread => thread.ManagedThreadId).ToList())
                                    {
                                        _aliveThreads.Remove(deadThreadID);
                                    }
                                    if (_aliveThreads.Count() >= 20)
                                    {
                                        String aliveThreads = "";
                                        lock (_aliveThreads)
                                        {
                                            aliveThreads = _aliveThreads.Values.ToList().Aggregate(aliveThreads, (current, value) => current + (value.Name + "[" + value.ManagedThreadId + "] "));
                                        }
                                        Log.Warn("Thread warning: " + aliveThreads);
                                    }
                                }
                            }

                            //Check for updated admins every minute
                            if (_enableAdKatsIntegration && (DateTime.UtcNow - lastAdminFetch).TotalSeconds > 60 && _threadsReady)
                            {
                                lastAdminFetch = DateTime.UtcNow;
                                ExecuteCommand("procon.protected.plugins.call", "AdKats", "FetchAuthorizedSoldiers", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                    {"caller_identity", "AdKatsLRT"},
                                    {"response_requested", true},
                                    {"response_class", "AdKatsLRT"},
                                    {"response_method", "ReceiveAdminList"},
                                    {"user_subset", "admin"}
                                }));
                            }

                            //Sleep 1 second between loops
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }
                        catch (Exception e)
                        {
                            Log.Exception("Error in keep-alive. Skipping current loop.", e);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Exception("Error while running keep-alive.", e);
                }
            }));
            //Start the thread
            statusMonitorThread.Start();
        }

        public void InitWaitHandles()
        {
            //Initializes all wait handles 
            _threadMasterWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _loadoutProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _playerProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            _battlelogCommWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void OpenAllHandles()
        {
            _threadMasterWaitHandle.Set();
            _loadoutProcessingWaitHandle.Set();
            _playerProcessingWaitHandle.Set();
            _battlelogCommWaitHandle.Set();
        }

        public void InitThreads()
        {
            try
            {
                _spawnProcessingThread = new Thread(ProcessingThreadLoop)
                {
                    IsBackground = true
                };

                _battlelogCommThread = new Thread(BattlelogCommThreadLoop)
                {
                    IsBackground = true
                };
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while initializing threads.", e);
            }
        }

        public void StartThreads()
        {
            Log.Debug("Entering StartThreads", 7);
            try
            {
                //Reset the master wait handle
                _threadMasterWaitHandle.Reset();
                //Start the spawn processing thread
                StartAndLogThread(_spawnProcessingThread);
                StartAndLogThread(_battlelogCommThread);
                _threadsReady = true;
            }
            catch (Exception e)
            {
                Log.Exception("Error while starting processing threads.", e);
            }
            Log.Debug("Exiting StartThreads", 7);
        }

        private void Disable()
        {
            //Call Disable
            ExecuteCommand("procon.protected.plugins.enable", "AdKatsLRT", "False");
            //Set enabled false so threads begin exiting
            _pluginEnabled = false;
            _threadsReady = false;
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv)
        {
            foreach (String env in lstPluginEnv)
            {
                Log.Debug("^9OnPluginLoadingEnv: " + env, 7);
            }
            switch (lstPluginEnv[1])
            {
                case "BF3":
                    _gameVersion = GameVersion.BF3;
                    break;
                case "BF4":
                    _gameVersion = GameVersion.BF4;
                    break;
            }
            Log.Debug("^1Game Version: " + _gameVersion, 1);
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset cpsSubset)
        {
            //Completely ignore this event if integrated with AdKats
            if (_enableAdKatsIntegration || !_pluginEnabled)
            {
                return;
            }
            Log.Debug("Entering OnListPlayers", 7);
            try
            {
                if (cpsSubset.Subset != CPlayerSubset.PlayerSubsetType.All)
                {
                    return;
                }
                lock (_playerDictionary)
                {
                    var validPlayers = new List<String>();
                    foreach (CPlayerInfo cPlayer in players)
                    {
                        //Check for glitched players
                        if (String.IsNullOrEmpty(cPlayer.GUID))
                        {
                            continue;
                        }
                        //Ready to parse
                        var aPlayer = new AdKatsSubscribedPlayer
                        {
                            ID = 0,
                            GUID = cPlayer.GUID,
                            PBGUID = null,
                            IP = null,
                            Name = cPlayer.SoldierName,
                            PersonaID = null,
                            ClanTag = null,
                            Online = true,
                            AA = false,
                            Ping = cPlayer.Ping,
                            Reputation = 0,
                            InfractionPoints = 0,
                            Role = "guest_default"
                        };
                        switch (cPlayer.Type)
                        {
                            case 0:
                                aPlayer.Type = "Player";
                                break;
                            case 1:
                                aPlayer.Type = "Spectator";
                                break;
                            case 2:
                                aPlayer.Type = "CommanderPC";
                                break;
                            case 3:
                                aPlayer.Type = "CommanderMobile";
                                break;
                            default:
                                Log.Error("Player type " + cPlayer.Type + " is not valid.");
                                break;
                        }
                        aPlayer.IsAdmin = false;
                        aPlayer.Reported = false;
                        aPlayer.Punished = false;
                        aPlayer.LoadoutForced = false;
                        aPlayer.LoadoutIgnored = false;
                        aPlayer.LastPunishment = TimeSpan.FromSeconds(0);
                        aPlayer.LastForgive = TimeSpan.FromSeconds(0);
                        aPlayer.LastAction = TimeSpan.FromSeconds(0);
                        aPlayer.SpawnedOnce = false;
                        aPlayer.ConversationPartner = null;
                        aPlayer.Kills = cPlayer.Kills;
                        aPlayer.Deaths = cPlayer.Deaths;
                        aPlayer.KDR = cPlayer.Kdr;
                        aPlayer.Rank = cPlayer.Rank;
                        aPlayer.Score = cPlayer.Score;
                        aPlayer.Squad = cPlayer.SquadID;
                        aPlayer.Team = cPlayer.TeamID;

                        validPlayers.Add(aPlayer.Name);

                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_playerDictionary.TryGetValue(aPlayer.Name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_playerLeftDictionary.TryGetValue(aPlayer.GUID, out dPlayer))
                            {
                                //They are returning, move their player object
                                Log.Debug(aPlayer.Name + " is returning.", 6);
                                dPlayer.Online = true;
                                dPlayer.WatchedVehicles.Clear();
                                _playerDictionary[aPlayer.Name] = dPlayer;
                                _playerLeftDictionary.Remove(aPlayer.GUID);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                Log.Debug(aPlayer.Name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _playerDictionary[aPlayer.Name] = aPlayer;
                            QueuePlayerForBattlelogInfoFetch(aPlayer);
                        }
                        else
                        {
                            dPlayer.Name = aPlayer.Name;
                            dPlayer.IP = aPlayer.IP;
                            dPlayer.AA = aPlayer.AA;
                            dPlayer.Ping = aPlayer.Ping;
                            dPlayer.Type = aPlayer.Type;
                            dPlayer.SpawnedOnce = aPlayer.SpawnedOnce;
                            dPlayer.Kills = aPlayer.Kills;
                            dPlayer.Deaths = aPlayer.Deaths;
                            dPlayer.KDR = aPlayer.KDR;
                            dPlayer.Rank = aPlayer.Rank;
                            dPlayer.Score = aPlayer.Score;
                            dPlayer.Squad = aPlayer.Squad;
                            dPlayer.Team = aPlayer.Team;
                        }
                    }
                    List<String> removeNames = _playerLeftDictionary.Where(pair => (DateTime.UtcNow - pair.Value.LastUsage).TotalMinutes > 120).Select(pair => pair.Key).ToList();
                    foreach (String removeName in removeNames)
                    {
                        _playerLeftDictionary.Remove(removeName);
                    }
                    if (_isTestingAuthorized && removeNames.Any())
                    {
                        Log.Warn(removeNames.Count() + " left players removed, " + _playerLeftDictionary.Count() + " still in cache.");
                    }
                    foreach (string playerName in _playerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            Log.Debug(aPlayer.Name + " removed from player list.", 6);
                            _playerDictionary.Remove(aPlayer.Name);
                            aPlayer.LastUsage = DateTime.UtcNow;
                            _playerLeftDictionary[aPlayer.GUID] = aPlayer;
                            aPlayer.LoadoutChecks = 0;
                        }
                        else
                        {
                            Log.Error("Unable to find " + playerName + " in online players when requesting removal.");
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _playerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured while listing players.", e);
            }
            Log.Debug("Exiting OnListPlayers", 7);
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            try
            {
                DateTime spawnTime = DateTime.UtcNow;
                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_playerDictionary.TryGetValue(soldierName, out aPlayer))
                    {
                        aPlayer.SpawnedOnce = true;
                        //Reject spawn processing if player has no persona ID
                        if (String.IsNullOrEmpty(aPlayer.PersonaID))
                        {
                            if (!_enableAdKatsIntegration)
                            {
                                QueuePlayerForBattlelogInfoFetch(aPlayer);
                            }
                            Log.Debug("Spawn process for " + aPlayer.Name + " cancelled because their Persona ID is not loaded yet.", 3);
                            return;
                        }
                        //Create process object
                        var processObject = new ProcessObject()
                        {
                            ProcessPlayer = aPlayer,
                            ProcessReason = "spawn",
                            ProcessTime = spawnTime
                        };
                        //Minimum wait time of 5 seconds
                        if (_loadoutProcessingQueue.Count >= 6)
                        {
                            QueueForProcessing(processObject);
                        }
                        else
                        {
                            var waitTime = TimeSpan.FromSeconds(5 - _loadoutProcessingQueue.Count);
                            if (waitTime.TotalSeconds <= 0.1)
                            {
                                waitTime = TimeSpan.FromSeconds(5);
                            }
                            Log.Debug("Waiting " + ((int)waitTime.TotalSeconds) + " seconds to process " + aPlayer.GetVerboseName() + " spawn.", 5);
                            //Start a delay thread
                            StartAndLogThread(new Thread(new ThreadStart(delegate
                            {
                                Thread.CurrentThread.Name = "LoadoutCheckDelay";
                                try
                                {
                                    Thread.Sleep(waitTime);
                                    QueueForProcessing(processObject);
                                }
                                catch (Exception e)
                                {
                                    Log.Exception("Error running loadout check delay thread.", e);
                                }
                                Thread.Sleep(100);
                                LogThreadExit();
                            })));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while handling player spawn.", e);
            }
        }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            Log.Debug("Entering OnPlayerLeft", 7);
            try
            {
                AdKatsSubscribedPlayer aPlayer;
                if (_playerDictionary.TryGetValue(playerInfo.SoldierName, out aPlayer))
                {
                    aPlayer.Online = false;
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while handling player left.", e);
            }
            Log.Debug("Exiting OnPlayerLeft", 7);
        }

        public void CallLoadoutCheckOnPlayer(params String[] parameters)
        {
            Log.Debug("CallLoadoutCheckOnPlayer starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    Log.Error("Call loadout check canceled. Parameters invalid.");
                    return;
                }
                String unparsedCommandJson = parameters[1];

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJson);

                var playerName = (String)decodedCommand["player_name"];
                var loadoutCheckReason = (String)decodedCommand["loadoutCheck_reason"];

                if (_threadsReady && _pluginEnabled && _firstPlayerListComplete)
                {
                    AdKatsSubscribedPlayer aPlayer;
                    if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                    {
                        Log.Write("Loadout check manually called on " + playerName + ".");
                        QueueForProcessing(new ProcessObject()
                        {
                            ProcessPlayer = aPlayer,
                            ProcessReason = loadoutCheckReason,
                            ProcessTime = DateTime.UtcNow,
                            ProcessManual = true
                        });
                    }
                    else
                    {
                        Log.Error("Attempted to call MANUAL loadout check on " + playerName + " without their player object loaded.");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while calling loadout check on player.", e);
            }
            Log.Debug("CallLoadoutCheckOnPlayer finished!", 6);
        }

        public void ReceiveAdminList(params String[] parameters)
        {
            Log.Debug("ReceiveAdminList starting!", 6);
            try
            {
                if (parameters.Length != 2)
                {
                    Log.Error("Online admin receiving cancelled. Parameters invalid.");
                    return;
                }
                String unparsedCommandJson = parameters[1];

                var decodedCommand = (Hashtable)JSON.JsonDecode(unparsedCommandJson);

                var unparsedAdminList = (String)decodedCommand["response_value"];

                String[] tempAdminList = CPluginVariable.DecodeStringArray(unparsedAdminList);
                foreach (String adminPlayerName in tempAdminList)
                {
                    if (!_adminList.Contains(adminPlayerName))
                    {
                        _adminList.Add(adminPlayerName);
                    }
                }
                _adminList.RemoveWhere(name => !tempAdminList.Contains(name));
            }
            catch (Exception e)
            {
                Log.Exception("Error while calling loadout check on player.", e);
            }
            Log.Debug("ReceiveAdminList finished!", 6);
        }

        private void QueueForProcessing(ProcessObject processObject)
        {
            Log.Debug("Entering QueueForProcessing", 7);
            try
            {
                if (processObject == null || processObject.ProcessPlayer == null)
                {
                    Log.Error("Attempted to process null object or player.");
                    return;
                }
                if (!processObject.ProcessPlayer.Online ||
                    String.IsNullOrEmpty(processObject.ProcessPlayer.PersonaID))
                {
                    Log.Debug(processObject.ProcessPlayer.Name + " queue cancelled. Player is not online, or has no persona ID.", 4);
                    return;
                }
                lock (_loadoutProcessingQueue)
                {
                    if (_loadoutProcessingQueue.Any(obj =>
                        obj != null &&
                        obj.ProcessPlayer != null &&
                        obj.ProcessPlayer.GUID == processObject.ProcessPlayer.GUID))
                    {
                        Log.Debug(processObject.ProcessPlayer.Name + " queue cancelled. Player already in queue.", 4);
                        return;
                    }
                    Int32 oldCount = _loadoutProcessingQueue.Count();
                    _loadoutProcessingQueue.Enqueue(processObject);
                    Log.Debug(processObject.ProcessPlayer.Name + " queued [" + oldCount + "->" + _loadoutProcessingQueue.Count + "] after " + Math.Round(DateTime.UtcNow.Subtract(processObject.ProcessTime).TotalSeconds, 2) + "s", 5);
                    _loadoutProcessingWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while queueing player for processing.", e);
            }
            Log.Debug("Exiting QueueForProcessing", 7);
        }

        public void ProcessingThreadLoop()
        {
            try
            {
                Log.Debug("SPROC: Starting Spawn Processing Thread", 1);
                Thread.CurrentThread.Name = "SpawnProcessing";
                while (true)
                {
                    try
                    {
                        Log.Debug("SPROC: Entering Spawn Processing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug("SPROC: Detected AdKatsLRT not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        if (_battlelogFetchQueue.Count() >= 5)
                        {
                            Log.Debug("loadout checks waiting on battlelog info fetches to complete.", 4);
                            _threadMasterWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                            continue;
                        }

                        if (_loadoutProcessingQueue.Count > 0)
                        {
                            ProcessObject processObject = null;
                            lock (_loadoutProcessingQueue)
                            {
                                //Dequeue the next object
                                Int32 oldCount = _loadoutProcessingQueue.Count();
                                ProcessObject importObject = _loadoutProcessingQueue.Dequeue();
                                if (importObject == null)
                                {
                                    Log.Error("Process object was null when entering player processing loop.");
                                    continue;
                                }
                                if (importObject.ProcessPlayer == null)
                                {
                                    Log.Error("Process player was null when entering player processing loop.");
                                    continue;
                                }
                                if (!importObject.ProcessPlayer.Online)
                                {
                                    continue;
                                }
                                var processDelay = DateTime.UtcNow.Subtract(importObject.ProcessTime);
                                if (DateTime.UtcNow.Subtract(importObject.ProcessTime).TotalSeconds > 30 && _loadoutProcessingQueue.Count < 3)
                                {
                                    Log.Warn(importObject.ProcessPlayer.GetVerboseName() + " took abnormally long to start processing. [" + FormatTimeString(processDelay, 2) + "]");
                                }
                                else
                                {
                                    Log.Debug(importObject.ProcessPlayer.Name + " dequeued [" + oldCount + "->" + _loadoutProcessingQueue.Count + "] after " + Math.Round(processDelay.TotalSeconds, 2) + "s", 5);
                                }
                                processObject = importObject;
                            }

                            //Grab the player
                            AdKatsSubscribedPlayer aPlayer = processObject.ProcessPlayer;

                            //Parse the reason for enforcement
                            Boolean fetchOnly = false;
                            Boolean fetchOnlyNotify = true;
                            Boolean trigger = false;
                            Boolean killOverride = false;
                            String reason = "";
                            if (processObject.ProcessReason == "fetch")
                            {
                                reason = "[fetch] ";
                                fetchOnly = true;
                            }
                            else if (aPlayer.LoadoutIgnored || processObject.ProcessReason == "ignored")
                            {
                                reason = "[ignored] ";
                                fetchOnly = true;
                                aPlayer.LoadoutIgnored = true;
                            }
                            else if (aPlayer.LoadoutForced || processObject.ProcessReason == "forced")
                            {
                                reason = "[forced] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if (aPlayer.Punished || processObject.ProcessReason == "punished")
                            {
                                reason = "[recently punished] ";
                                trigger = true;
                                killOverride = true;
                            }
                            else if ((aPlayer.Reported || processObject.ProcessReason == "reported") && aPlayer.Reputation <= 0)
                            {
                                reason = "[reported] ";
                                trigger = true;
                            }
                            else if (aPlayer.InfractionPoints >= _triggerEnforcementMinimumInfractionPoints && aPlayer.LastPunishment.TotalDays < 60 && aPlayer.Reputation <= 0)
                            {
                                reason = "[" + aPlayer.InfractionPoints + " infractions] ";
                                trigger = true;
                            }
                            else if (processObject.ProcessReason == "vehiclekill")
                            {
                                reason = "[vehicle kill] ";
                            }
                            else if (processObject.ProcessReason == "spawn")
                            {
                                reason = "[spawn] ";
                            }
                            else if (processObject.ProcessReason == "listing")
                            {
                                reason = "[join] ";
                            }
                            else
                            {
                                Log.Error("Unknown reason for processing player. Cancelling processing.");
                                continue;
                            }

                            Log.Debug("Processing " + reason + aPlayer.GetVerboseName(), 4);

                            if (!fetchOnly)
                            {
                                //Process is not fetch only, check to see if we can skip this player
                                Boolean fetch = true;
                                String rejectFetchReason = "Loadout fetches cancelled. No reason given.";
                                if (!trigger)
                                {
                                    if (fetch &&
                                        (aPlayer.Reputation >= 50 && !_spawnEnforcementActOnReputablePlayers))
                                    {
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player is reputable.";
                                        if (_displayWeaponPopularity)
                                        {
                                            fetchOnly = true;
                                            fetchOnlyNotify = false;
                                        }
                                        else
                                        {
                                            fetch = false;
                                        }
                                    }
                                    if (fetch &&
                                        (aPlayer.IsAdmin && !_spawnEnforcementActOnAdmins))
                                    {
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player is admin.";
                                        if (_displayWeaponPopularity)
                                        {
                                            fetchOnly = true;
                                            fetchOnlyNotify = false;
                                        }
                                        else
                                        {
                                            fetch = false;
                                        }
                                    }
                                    //Special case for large servers to reduce request frequency
                                    if (fetch &&
                                        !_highRequestVolume &&
                                        aPlayer.LoadoutChecks > ((aPlayer.Reputation > 0) ? (0) : (3)) &&
                                        aPlayer.LoadoutValid &&
                                        aPlayer.SkippedChecks < 4)
                                    {
                                        aPlayer.SkippedChecks++;
                                        rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player clean after " + aPlayer.LoadoutChecks + " checks. " + aPlayer.SkippedChecks + " current skips.";
                                        fetch = false;
                                    }
                                }
                                if (fetch &&
                                    (_Whitelist.Contains(aPlayer.Name) ||
                                    _Whitelist.Contains(aPlayer.GUID) ||
                                    _Whitelist.Contains(aPlayer.PBGUID) ||
                                    _Whitelist.Contains(aPlayer.IP)))
                                {
                                    rejectFetchReason = aPlayer.Name + " loadout actions cancelled. Player on whitelist.";
                                    if (_displayWeaponPopularity)
                                    {
                                        fetchOnly = true;
                                        fetchOnlyNotify = false;
                                    }
                                    else
                                    {
                                        fetch = false;
                                    }
                                }
                                if (!fetch)
                                {
                                    if (_enableAdKatsIntegration)
                                    {
                                        //Inform AdKats of the check rejection
                                        StartAndLogThread(new Thread(new ThreadStart(delegate
                                        {
                                            Thread.CurrentThread.Name = "AdKatsInform";
                                            Thread.Sleep(50);
                                            ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                            {"caller_identity", "AdKatsLRT"},
                                            {"response_requested", false},
                                            {"loadout_player", aPlayer.Name},
                                            {"loadout_valid", true},
                                            {"loadout_spawnValid", true},
                                            {"loadout_acted", false},
                                            {"loadout_items", rejectFetchReason},
                                            {"loadout_items_long", rejectFetchReason},
                                            {"loadout_deniedItems", rejectFetchReason}
                                        }));
                                            Thread.Sleep(50);
                                            LogThreadExit();
                                        })));
                                    }
                                    Log.Debug(rejectFetchReason, 3);
                                    continue;
                                }
                            }

                            //Fetch the loadout
                            AdKatsLoadout loadout = GetPlayerLoadout(aPlayer.PersonaID);
                            if (loadout == null)
                            {
                                continue;
                            }
                            aPlayer.Loadout = loadout;
                            aPlayer.LoadoutChecks++;
                            aPlayer.SkippedChecks = 0;

                            //Show the loadout contents
                            String primaryMessage = loadout.KitItemPrimary.Slug + " [" + loadout.KitItemPrimary.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemPrimary.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String sidearmMessage = loadout.KitItemSidearm.Slug + " [" + loadout.KitItemSidearm.AccessoriesAssigned.Values.Aggregate("", (currentString, acc) => currentString + TrimStart(acc.Slug, loadout.KitItemSidearm.Slug).Trim() + ", ").Trim().TrimEnd(',') + "]";
                            String gadgetMessage = "[" + loadout.KitGadget1.Slug + ", " + loadout.KitGadget2.Slug + "]";
                            String grenadeMessage = "[" + loadout.KitGrenade.Slug + "]";
                            String knifeMessage = "[" + loadout.KitKnife.Slug + "]";
                            String loadoutLongMessage = "Player " + loadout.Name + " processed as " + loadout.SelectedKit.KitType + " with primary " + primaryMessage + " sidearm " + sidearmMessage + " gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            String loadoutShortMessage = "Primary [" + loadout.KitItemPrimary.Slug + "] sidearm [" + loadout.KitItemSidearm.Slug + "] gadgets " + gadgetMessage + " grenade " + grenadeMessage + " and knife " + knifeMessage;
                            Log.Debug(loadoutLongMessage, 4);

                            if (fetchOnly && fetchOnlyNotify)
                            {
                                //Inform AdKats of the loadout
                                StartAndLogThread(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "AdKatsInform";
                                    Thread.Sleep(100);
                                    Log.Debug("Informing AdKats of " + aPlayer.GetVerboseName() + " fetched loadout.", 3);
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"loadout_player", loadout.Name},
                                        {"loadout_valid", true},
                                        {"loadout_spawnValid", true},
                                        {"loadout_acted", false},
                                        {"loadout_items", loadoutShortMessage},
                                        {"loadout_items_long", loadoutLongMessage},
                                        {"loadout_deniedItems", ""}
                                    }));
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                                continue;
                            }

                            //Action taken?
                            Boolean acted = false;

                            HashSet<String> specificMessages = new HashSet<String>();
                            HashSet<String> spawnSpecificMessages = new HashSet<String>();
                            HashSet<String> vehicleSpecificMessages = new HashSet<String>();
                            Boolean loadoutValid = true;
                            Boolean spawnLoadoutValid = true;
                            Boolean vehicleLoadoutValid = true;

                            if (_serverInfo.InfoObject.GameMode != "GunMaster0" &&
                                _serverInfo.InfoObject.GameMode != "GunMaster1" &&
                                (!_restrictSpecificMapModes || _restrictedMapModes.ContainsKey(_serverInfo.InfoObject.GameMode + "|" + _serverInfo.InfoObject.Map)))
                            {
                                if (trigger)
                                {
                                    foreach (var warsawDeniedIDMessage in _warsawInvalidLoadoutIDMessages)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedIDMessage.Key))
                                        {
                                            loadoutValid = false;
                                            if (!specificMessages.Contains(warsawDeniedIDMessage.Value))
                                            {
                                                specificMessages.Add(warsawDeniedIDMessage.Value);
                                            }
                                        }
                                    }

                                    foreach (var warsawDeniedID in _warsawSpawnDeniedIDs)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                        {
                                            spawnLoadoutValid = false;
                                            if (!spawnSpecificMessages.Contains(_warsawInvalidLoadoutIDMessages[warsawDeniedID]))
                                            {
                                                spawnSpecificMessages.Add(_warsawInvalidLoadoutIDMessages[warsawDeniedID]);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    foreach (var warsawDeniedID in _warsawSpawnDeniedIDs)
                                    {
                                        if (loadout.AllKitItemIDs.Contains(warsawDeniedID))
                                        {
                                            loadoutValid = false;
                                            spawnLoadoutValid = false;
                                            if (!spawnSpecificMessages.Contains(_warsawInvalidLoadoutIDMessages[warsawDeniedID]))
                                            {
                                                spawnSpecificMessages.Add(_warsawInvalidLoadoutIDMessages[warsawDeniedID]);
                                            }
                                        }
                                    }
                                }

                                foreach (var warsawDeniedIDMessage in _warsawInvalidVehicleLoadoutIDMessages)
                                {
                                    if (_spawnEnforceAllVehicles)
                                    {
                                        if (loadout.VehicleItems.ContainsKey(warsawDeniedIDMessage.Key))
                                        {
                                            loadoutValid = false;
                                            vehicleLoadoutValid = false;
                                            if (!vehicleSpecificMessages.Contains(warsawDeniedIDMessage.Value))
                                            {
                                                vehicleSpecificMessages.Add(warsawDeniedIDMessage.Value);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //Wow this needs optimization...
                                        foreach (String category in aPlayer.WatchedVehicles)
                                        {
                                            WarsawVehicle vehicle;
                                            if (!loadout.LoadoutVehicles.TryGetValue(category, out vehicle))
                                            {
                                                Log.Error("Could not fetch used vehicle " + category + " from player loadout, skipping.");
                                                continue;
                                            }
                                            if ((vehicle.AssignedPrimary != null && vehicle.AssignedPrimary.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedSecondary != null && vehicle.AssignedSecondary.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedOptic != null && vehicle.AssignedOptic.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedCountermeasure != null && vehicle.AssignedCountermeasure.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedUpgrade != null && vehicle.AssignedUpgrade.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedSecondaryGunner != null && vehicle.AssignedSecondaryGunner.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedOpticGunner != null && vehicle.AssignedOpticGunner.WarsawID == warsawDeniedIDMessage.Key) ||
                                                (vehicle.AssignedUpgradeGunner != null && vehicle.AssignedUpgradeGunner.WarsawID == warsawDeniedIDMessage.Key))
                                            {
                                                loadoutValid = false;
                                                vehicleLoadoutValid = false;
                                                if (!vehicleSpecificMessages.Contains(warsawDeniedIDMessage.Value))
                                                {
                                                    vehicleSpecificMessages.Add(warsawDeniedIDMessage.Value);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            Boolean act = true;
                            if (!trigger && !spawnLoadoutValid)
                            {
                                if (act && (processObject.ProcessPlayer.Reputation >= 50 && !_spawnEnforcementActOnReputablePlayers))
                                {
                                    Log.Debug(processObject.ProcessPlayer.Name + " spawn loadout enforcement cancelled. Player is reputable.", 4);
                                    act = false;
                                }
                                if (act && (processObject.ProcessPlayer.IsAdmin && !_spawnEnforcementActOnAdmins))
                                {
                                    Log.Debug(processObject.ProcessPlayer.Name + " spawn loadout enforcement cancelled. Player is admin.", 4);
                                    act = false;
                                }
                            }
                            if (act && (_Whitelist.Contains(processObject.ProcessPlayer.Name) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.GUID) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.PBGUID) ||
                                _Whitelist.Contains(processObject.ProcessPlayer.IP)))
                            {
                                Log.Debug(processObject.ProcessPlayer.Name + " loadout enforcement cancelled. Player on whitelist.", 4);
                                act = false;
                            }
                            if (!act)
                            {
                                if (_enableAdKatsIntegration)
                                {
                                    //Inform AdKats of the loadout
                                    StartAndLogThread(new Thread(new ThreadStart(delegate
                                    {
                                        Thread.CurrentThread.Name = "AdKatsInform";
                                        Thread.Sleep(100);
                                        ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                                    {"caller_identity", "AdKatsLRT"},
                                                    {"response_requested", false},
                                                    {"loadout_player", loadout.Name},
                                                    {"loadout_valid", loadoutValid},
                                                    {"loadout_spawnValid", spawnLoadoutValid},
                                                    {"loadout_acted", false},
                                                    {"loadout_items", loadoutShortMessage},
                                                    {"loadout_items_long", loadoutLongMessage},
                                                    {"loadout_deniedItems", ""}
                                                }));
                                        Thread.Sleep(100);
                                        LogThreadExit();
                                    })));
                                }
                                continue;
                            }

                            aPlayer.LoadoutEnforced = true;
                            String deniedWeapons = String.Empty;
                            String spawnDeniedWeapons = String.Empty;
                            if (!loadoutValid)
                            {
                                //Fill the denied messages
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemPrimary.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitItemSidearm.WarsawID))
                                {
                                    deniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                deniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawInvalidLoadoutIDMessages.ContainsKey(weaponAccessory.WarsawID)).Aggregate(deniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget1.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGadget2.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitGrenade.WarsawID))
                                {
                                    deniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_warsawInvalidLoadoutIDMessages.ContainsKey(loadout.KitKnife.WarsawID))
                                {
                                    deniedWeapons += loadout.KitKnife.Slug.ToUpper() + ", ";
                                }
                                //Fill the spawn denied messages
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitItemPrimary.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemPrimary.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemPrimary.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitItemSidearm.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitItemSidearm.Slug.ToUpper() + ", ";
                                }
                                spawnDeniedWeapons = loadout.KitItemSidearm.AccessoriesAssigned.Values.Where(weaponAccessory => _warsawSpawnDeniedIDs.Contains(weaponAccessory.WarsawID)).Aggregate(spawnDeniedWeapons, (current, weaponAccessory) => current + (weaponAccessory.Slug.ToUpper() + ", "));
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGadget1.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget1.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGadget2.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGadget2.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitGrenade.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitGrenade.Slug.ToUpper() + ", ";
                                }
                                if (_warsawSpawnDeniedIDs.Contains(loadout.KitKnife.WarsawID))
                                {
                                    spawnDeniedWeapons += loadout.KitKnife.Slug.ToUpper() + ", ";
                                }
                                //Trim the messages
                                deniedWeapons = deniedWeapons.Trim().TrimEnd(',');
                                spawnDeniedWeapons = spawnDeniedWeapons.Trim().TrimEnd(',');

                                //Decide whether to kill the player
                                Boolean adminsOnline = AdminsOnline();
                                if (!vehicleLoadoutValid || !spawnLoadoutValid || killOverride || (!adminsOnline && trigger))
                                {
                                    //Player will be killed
                                    acted = true;
                                    Log.Debug(loadout.Name + ((processObject.ProcessReason == "listing") ? (" JOIN") : (" SPAWN")) + " KILLED for invalid loadout.", 1);
                                    if (processObject.ProcessReason != "listing")
                                    {
                                        aPlayer.LoadoutKills++;
                                    }
                                    if (aPlayer.SpawnedOnce)
                                    {
                                        //Start a repeat kill
                                        StartAndLogThread(new Thread(new ThreadStart(delegate
                                        {
                                            Thread.CurrentThread.Name = "PlayerRepeatKill";
                                            Thread.Sleep(100);
                                            for (Int32 index = 0; index < 15; index++)
                                            {
                                                ExecuteCommand("procon.protected.send", "admin.killPlayer", loadout.Name);
                                                Thread.Sleep(500);
                                            }
                                            Thread.Sleep(100);
                                            LogThreadExit();
                                        })));
                                    }
                                    else
                                    {
                                        //Perform a single kill
                                        ExecuteCommand("procon.protected.send", "admin.killPlayer", loadout.Name);
                                    }

                                    String adminMessage = reason + aPlayer.GetVerboseName() + " killed for ";
                                    HashSet<String> tellMessages = new HashSet<String>();
                                    if (trigger && (killOverride || !adminsOnline))
                                    {
                                        //Manual trigger or no admins online, enforce all denied weapons
                                        adminMessage += "denied items [" + deniedWeapons + "]";
                                        PlayerSayMessage(aPlayer.Name, reason + aPlayer.GetVerboseName() + " please remove [" + deniedWeapons + "] from your loadout.");
                                        foreach (var specificMessage in specificMessages)
                                        {
                                            if (!tellMessages.Contains(specificMessage))
                                            {
                                                tellMessages.Add(specificMessage);
                                            }
                                        }
                                    }
                                    else if (!spawnLoadoutValid)
                                    {
                                        //Loadout enforcement was not triggered, enforce spawn denied weapons only
                                        PlayerSayMessage(aPlayer.Name, reason + aPlayer.GetVerboseName() + " please remove [" + spawnDeniedWeapons + "] from your loadout.");
                                        foreach (var spawnSpecificMessage in spawnSpecificMessages)
                                        {
                                            if (!tellMessages.Contains(spawnSpecificMessage))
                                            {
                                                tellMessages.Add(spawnSpecificMessage);
                                            }
                                        }
                                    }
                                    if (!vehicleLoadoutValid)
                                    {
                                        if (killOverride)
                                        {
                                            adminMessage += ", and ";
                                        }
                                        adminMessage += "invalid vehicle loadout";
                                        foreach (var vehicleSpecificMessage in vehicleSpecificMessages)
                                        {
                                            if (!tellMessages.Contains(vehicleSpecificMessage))
                                            {
                                                tellMessages.Add(vehicleSpecificMessage);
                                            }
                                        }
                                    }
                                    adminMessage += ".";
                                    //Inform Admins
                                    if (killOverride || !vehicleLoadoutValid)
                                    {
                                        OnlineAdminSayMessage(adminMessage);
                                    }
                                    //Set max denied items if player has been killed
                                    if (tellMessages.Count > aPlayer.MaxDeniedItems && aPlayer.LoadoutKills > 0)
                                    {
                                        aPlayer.MaxDeniedItems = tellMessages.Count;
                                    }
                                    //Inform Player
                                    Int32 tellIndex = 1;
                                    foreach (String tellMessage in tellMessages)
                                    {
                                        String prefix = ((tellMessages.Count > 1) ? ("(" + (tellIndex++) + "/" + tellMessages.Count + ") ") : (""));
                                        PlayerTellMessage(loadout.Name, prefix + tellMessage);
                                        if (tellMessages.Count > 1)
                                        {
                                            Thread.Sleep(2000);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (!aPlayer.LoadoutValid)
                                {
                                    PlayerSayMessage(aPlayer.Name, aPlayer.GetVerboseName() + " thank you for fixing your loadout.");
                                    if (killOverride)
                                    {
                                        OnlineAdminSayMessage(reason + aPlayer.GetVerboseName() + " fixed their loadout.");
                                    }
                                }
                                else if (processObject.ProcessManual)
                                {
                                    OnlineAdminSayMessage(aPlayer.GetVerboseName() + "'s has no denied items.");
                                }
                            }
                            if (_enableAdKatsIntegration)
                            {
                                //Inform AdKats of the loadout
                                StartAndLogThread(new Thread(new ThreadStart(delegate
                                {
                                    Thread.CurrentThread.Name = "AdKatsInform";
                                    Thread.Sleep(100);
                                    ExecuteCommand("procon.protected.plugins.call", "AdKats", "ReceiveLoadoutValidity", "AdKatsLRT", JSON.JsonEncode(new Hashtable {
                                        {"caller_identity", "AdKatsLRT"},
                                        {"response_requested", false},
                                        {"loadout_player", loadout.Name},
                                        {"loadout_valid", loadoutValid},
                                        {"loadout_spawnValid", spawnLoadoutValid},
                                        {"loadout_acted", acted},
                                        {"loadout_items", loadoutShortMessage},
                                        {"loadout_items_long", loadoutLongMessage},
                                        {"loadout_deniedItems", deniedWeapons}
                                    }));
                                    Thread.Sleep(100);
                                    LogThreadExit();
                                })));
                            }
                            aPlayer.LoadoutValid = loadoutValid;
                            lock (_playerDictionary)
                            {
                                Int32 totalPlayerCount = _playerDictionary.Count + _playerLeftDictionary.Count;
                                Int32 countKills = _playerDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills) + _playerLeftDictionary.Values.Sum(dPlayer => dPlayer.LoadoutKills);
                                Int32 countEnforced = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutEnforced) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutEnforced);
                                Int32 countKilled = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0);
                                Int32 countFixed = _playerDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && dPlayer.LoadoutValid) + _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && dPlayer.LoadoutValid);
                                Int32 countQuit = _playerLeftDictionary.Values.Count(dPlayer => dPlayer.LoadoutKills > 0 && !dPlayer.LoadoutValid);
                                Boolean displayStats = (_countKilled != countKilled) ||
                                                       (_countFixed != countFixed) ||
                                                       (_countQuit != countQuit);
                                _countKilled = countKilled;
                                _countFixed = countFixed;
                                _countQuit = countQuit;
                                Double percentEnforced = Math.Round((countEnforced / (Double)totalPlayerCount) * 100.0);
                                Double percentKilled = Math.Round((countKilled / (Double)totalPlayerCount) * 100.0);
                                Double percentFixed = Math.Round((countFixed / (Double)countKilled) * 100.0);
                                Double percentRaged = Math.Round((countQuit / (Double)countKilled) * 100.0);
                                Double denialKpm = Math.Round(countKills / (DateTime.UtcNow - _pluginStartTime).TotalMinutes, 2);
                                Double killsPerDenial = Math.Round(countKills / (Double)countKilled, 2);
                                Double avgDeniedItems = Math.Round((_playerDictionary.Values.Sum(dPlayer => dPlayer.MaxDeniedItems) + _playerLeftDictionary.Values.Sum(dPlayer => dPlayer.MaxDeniedItems)) / (Double)countKilled, 2);
                                if (displayStats)
                                {
                                    Log.Debug("(" + countEnforced + "/" + totalPlayerCount + ") " + percentEnforced + "% enforced. " + "(" + countKilled + "/" + totalPlayerCount + ") " + percentKilled + "% killed. " + "(" + countFixed + "/" + countKilled + ") " + percentFixed + "% fixed. " + "(" + countQuit + "/" + countKilled + ") " + percentRaged + "% quit. " + denialKpm + " denial KPM. " + killsPerDenial + " kills per denial. " + avgDeniedItems + " AVG denied items.", 2);
                                }
                            }
                            Log.Debug(_loadoutProcessingQueue.Count + " players still in queue.", 3);
                            Log.Debug(processObject.ProcessPlayer.Name + " processed after " + Math.Round(DateTime.UtcNow.Subtract(processObject.ProcessTime).TotalSeconds, 2) + "s", 5);
                        }
                        else
                        {
                            //Wait for input
                            _loadoutProcessingWaitHandle.Reset();
                            _loadoutProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Exception("Spawn processing thread aborted. Exiting.", e);
                            break;
                        }
                        Log.Exception("Error occured in spawn processing thread. Skipping current loop.", e);
                    }
                }
                Log.Debug("SPROC: Ending Spawn Processing Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured in kill processing thread.", e);
            }
        }

        private void QueuePlayerForBattlelogInfoFetch(AdKatsSubscribedPlayer aPlayer)
        {
            Log.Debug("Entering QueuePlayerForBattlelogInfoFetch", 6);
            try
            {
                Log.Debug("Preparing to queue player for battlelog info fetch.", 6);
                if (_battlelogFetchQueue.Any(bPlayer => bPlayer.GUID == aPlayer.GUID))
                {
                    return;
                }
                lock (_battlelogFetchQueue)
                {
                    _battlelogFetchQueue.Enqueue(aPlayer);
                    Log.Debug("Player queued for battlelog info fetch.", 6);
                    _battlelogCommWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while queuing player for battlelog info fetch.", e);
            }
            Log.Debug("Exiting QueuePlayerForBattlelogInfoFetch", 6);
        }

        public void BattlelogCommThreadLoop()
        {
            try
            {
                Log.Debug("BTLOG: Starting Battlelog Comm Thread", 1);
                Thread.CurrentThread.Name = "BattlelogComm";
                while (true)
                {
                    try
                    {
                        Log.Debug("BTLOG: Entering Battlelog Comm Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug("BTLOG: Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }
                        //Sleep for 10ms
                        _threadMasterWaitHandle.WaitOne(10);

                        //Handle Inbound player fetches
                        if (_battlelogFetchQueue.Count > 0)
                        {
                            Queue<AdKatsSubscribedPlayer> unprocessedPlayers;
                            lock (_battlelogFetchQueue)
                            {
                                Log.Debug("BTLOG: Inbound players found. Grabbing.", 6);
                                //Grab all items in the queue
                                unprocessedPlayers = new Queue<AdKatsSubscribedPlayer>(_battlelogFetchQueue.ToArray());
                                //Clear the queue for next run
                                _battlelogFetchQueue.Clear();
                            }
                            //Loop through all players in order that they came in
                            while (unprocessedPlayers.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                Log.Debug("BTLOG: Preparing to fetch battlelog info for player", 6);
                                //Dequeue the record
                                AdKatsSubscribedPlayer aPlayer = unprocessedPlayers.Dequeue();
                                //Run the appropriate action
                                FetchPlayerBattlelogInformation(aPlayer);
                            }
                        }
                        else
                        {
                            //Wait for new actions
                            _battlelogCommWaitHandle.Reset();
                            _battlelogCommWaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.Exception("Battlelog comm thread aborted. Exiting.", e);
                            break;
                        }
                        Log.Exception("Error occured in Battlelog comm thread. Skipping current loop.", e);
                    }
                }
                Log.Debug("BTLOG: Ending Battlelog Comm Thread", 1);
                LogThreadExit();
            }
            catch (Exception e)
            {
                Log.Exception("Error occured in battlelog comm thread.", e);
            }
        }

        public void FetchPlayerBattlelogInformation(AdKatsSubscribedPlayer aPlayer)
        {
            try
            {
                if (!String.IsNullOrEmpty(aPlayer.PersonaID))
                {
                    return;
                }
                if (String.IsNullOrEmpty(aPlayer.Name))
                {
                    Log.Error("Attempted to get battlelog information of nameless player.");
                    return;
                }
                try
                {
                    DoBattlelogWait();
                    var httpClient = new HttpClient();
                    String personaResponse = httpClient.GetStringAsync("http://battlelog.battlefield.com/bf4/user/" + aPlayer.Name).Result;
                    Match pid = Regex.Match(personaResponse, @"bf4/soldier/" + aPlayer.Name + @"/stats/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (!pid.Success)
                    {
                        Log.Error("Could not find persona ID for " + aPlayer.Name);
                        return;
                    }
                    aPlayer.PersonaID = pid.Groups[1].Value.Trim();
                    Log.Debug("Persona ID fetched for " + aPlayer.Name, 4);
                    QueueForProcessing(new ProcessObject()
                    {
                        ProcessPlayer = aPlayer,
                        ProcessReason = "listing",
                        ProcessTime = DateTime.UtcNow
                    });
                    DoBattlelogWait();
                    String overviewResponse = ("http://battlelog.battlefield.com/bf4/warsawoverviewpopulate/" + aPlayer.PersonaID + "/1/")
                        .GetStringAsync().Result;

                    Hashtable json = (Hashtable)JSON.JsonDecode(overviewResponse);
                    Hashtable data = (Hashtable)json["data"];
                    Hashtable info = null;
                    if (!data.ContainsKey("viewedPersonaInfo") || (info = (Hashtable)data["viewedPersonaInfo"]) == null)
                    {
                        aPlayer.ClanTag = String.Empty;
                        Log.Debug("Could not find BF4 clan tag for " + aPlayer.Name, 4);
                    }
                    else
                    {
                        String tag = String.Empty;
                        if (!info.ContainsKey("tag") || String.IsNullOrEmpty(tag = (String)info["tag"]))
                        {
                            aPlayer.ClanTag = String.Empty;
                            Log.Debug("Could not find BF4 clan tag for " + aPlayer.Name, 4);
                        }
                        else
                        {
                            aPlayer.ClanTag = tag;
                            Log.Debug("Clan tag [" + aPlayer.ClanTag + "] found for " + aPlayer.Name, 4);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is HttpRequestException)
                    {
                        Log.Warn("Issue connecting to battlelog.");
                        _lastBattlelogAction = DateTime.UtcNow.AddSeconds(30);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while fetching battlelog information for " + aPlayer.Name, e);
            }
        }

        public void ReceiveOnlineSoldiers(params String[] parameters)
        {
            Log.Debug("ReceiveOnlineSoldiers starting!", 6);
            try
            {
                if (!_enableAdKatsIntegration)
                {
                    return;
                }
                if (parameters.Length != 2)
                {
                    Log.Error("Online soldier handling canceled. Parameters invalid.");
                    return;
                }
                String unparsedResponseJSON = parameters[1];

                var decodedResponse = (Hashtable)JSON.JsonDecode(unparsedResponseJSON);

                var decodedSoldierList = (ArrayList)decodedResponse["response_value"];
                if (decodedSoldierList == null)
                {
                    Log.Error("Soldier params could not be properly converted from JSON. Unable to continue.");
                    return;
                }
                lock (_playerDictionary)
                {
                    var validPlayers = new List<String>();
                    foreach (Hashtable soldierHashtable in decodedSoldierList)
                    {
                        var aPlayer = new AdKatsSubscribedPlayer
                        {
                            ID = Convert.ToInt64((Double)soldierHashtable["player_id"]),
                            GUID = (String)soldierHashtable["player_guid"],
                            PBGUID = (String)soldierHashtable["player_pbguid"],
                            IP = (String)soldierHashtable["player_ip"],
                            Name = (String)soldierHashtable["player_name"],
                            PersonaID = (String)soldierHashtable["player_personaID"],
                            ClanTag = (String)soldierHashtable["player_clanTag"],
                            Online = (Boolean)soldierHashtable["player_online"],
                            AA = (Boolean)soldierHashtable["player_aa"],
                            Ping = (Double)soldierHashtable["player_ping"],
                            Reputation = (Double)soldierHashtable["player_reputation"],
                            InfractionPoints = Convert.ToInt32((Double)soldierHashtable["player_infractionPoints"]),
                            Role = (String)soldierHashtable["player_role"],
                            Type = (String)soldierHashtable["player_type"],
                            IsAdmin = (Boolean)soldierHashtable["player_isAdmin"],
                            Reported = (Boolean)soldierHashtable["player_reported"],
                            Punished = (Boolean)soldierHashtable["player_punished"],
                            LoadoutForced = (Boolean)soldierHashtable["player_loadout_forced"],
                            LoadoutIgnored = (Boolean)soldierHashtable["player_loadout_ignored"]
                        };
                        var lastPunishment = (Double)soldierHashtable["player_lastPunishment"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastPunishment = TimeSpan.FromSeconds(lastPunishment);
                        }
                        var lastForgive = (Double)soldierHashtable["player_lastForgive"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastForgive = TimeSpan.FromSeconds(lastForgive);
                        }
                        var lastAction = (Double)soldierHashtable["player_lastAction"];
                        if (lastPunishment > 0)
                        {
                            aPlayer.LastAction = TimeSpan.FromSeconds(lastAction);
                        }
                        aPlayer.SpawnedOnce = (Boolean)soldierHashtable["player_spawnedOnce"];
                        aPlayer.ConversationPartner = (String)soldierHashtable["player_conversationPartner"];
                        aPlayer.Kills = Convert.ToInt32((Double)soldierHashtable["player_kills"]);
                        aPlayer.Deaths = Convert.ToInt32((Double)soldierHashtable["player_deaths"]);
                        aPlayer.KDR = (Double)soldierHashtable["player_kdr"];
                        aPlayer.Rank = Convert.ToInt32((Double)soldierHashtable["player_rank"]);
                        aPlayer.Score = Convert.ToInt32((Double)soldierHashtable["player_score"]);
                        aPlayer.Squad = Convert.ToInt32((Double)soldierHashtable["player_squad"]);
                        aPlayer.Team = Convert.ToInt32((Double)soldierHashtable["player_team"]);

                        validPlayers.Add(aPlayer.Name);

                        Boolean process = false;
                        AdKatsSubscribedPlayer dPlayer;
                        Boolean newPlayer = false;
                        //Are they online?
                        if (!_playerDictionary.TryGetValue(aPlayer.Name, out dPlayer))
                        {
                            //Not online. Are they returning?
                            if (_playerLeftDictionary.TryGetValue(aPlayer.GUID, out dPlayer))
                            {
                                //They are returning, move their player object
                                Log.Debug(aPlayer.Name + " is returning.", 6);
                                dPlayer.Online = true;
                                _playerDictionary[aPlayer.Name] = dPlayer;
                                _playerLeftDictionary.Remove(dPlayer.GUID);
                            }
                            else
                            {
                                //Not online or returning. New player.
                                Log.Debug(aPlayer.Name + " is newly joining.", 6);
                                newPlayer = true;
                            }
                        }
                        if (newPlayer)
                        {
                            _playerDictionary[aPlayer.Name] = aPlayer;
                            dPlayer = aPlayer;
                            process = true;
                        }
                        else
                        {
                            dPlayer.Name = aPlayer.Name;
                            dPlayer.IP = aPlayer.IP;
                            dPlayer.AA = aPlayer.AA;
                            if (String.IsNullOrEmpty(dPlayer.PersonaID) && !String.IsNullOrEmpty(aPlayer.PersonaID))
                            {
                                process = true;
                            }
                            dPlayer.PersonaID = aPlayer.PersonaID;
                            dPlayer.ClanTag = aPlayer.ClanTag;
                            dPlayer.Online = aPlayer.Online;
                            dPlayer.Ping = aPlayer.Ping;
                            dPlayer.Reputation = aPlayer.Reputation;
                            dPlayer.InfractionPoints = aPlayer.InfractionPoints;
                            dPlayer.Role = aPlayer.Role;
                            dPlayer.Type = aPlayer.Type;
                            dPlayer.IsAdmin = aPlayer.IsAdmin;
                            dPlayer.Reported = aPlayer.Reported;
                            dPlayer.Punished = aPlayer.Punished;
                            dPlayer.LoadoutForced = aPlayer.LoadoutForced;
                            dPlayer.LoadoutIgnored = aPlayer.LoadoutIgnored;
                            dPlayer.SpawnedOnce = aPlayer.SpawnedOnce;
                            dPlayer.ConversationPartner = aPlayer.ConversationPartner;
                            dPlayer.Kills = aPlayer.Kills;
                            dPlayer.Deaths = aPlayer.Deaths;
                            dPlayer.KDR = aPlayer.KDR;
                            dPlayer.Rank = aPlayer.Rank;
                            dPlayer.Score = aPlayer.Score;
                            dPlayer.Squad = aPlayer.Squad;
                            dPlayer.Team = aPlayer.Team;
                        }
                        dPlayer.LastUsage = DateTime.UtcNow;
                        if (process)
                        {
                            QueueForProcessing(new ProcessObject()
                            {
                                ProcessPlayer = dPlayer,
                                ProcessReason = "listing",
                                ProcessTime = DateTime.UtcNow
                            });
                        }
                        Log.Debug(aPlayer.Name + " online after listing: " + _playerDictionary.ContainsKey(aPlayer.Name), 7);
                    }
                    foreach (string playerName in _playerDictionary.Keys.Where(playerName => !validPlayers.Contains(playerName)).ToList())
                    {
                        AdKatsSubscribedPlayer aPlayer;
                        if (_playerDictionary.TryGetValue(playerName, out aPlayer))
                        {
                            Log.Debug(aPlayer.Name + " removed from player list.", 6);
                            aPlayer.LastUsage = DateTime.UtcNow;
                            _playerDictionary.Remove(aPlayer.Name);
                            _playerLeftDictionary[aPlayer.GUID] = aPlayer;
                            aPlayer.LoadoutChecks = 0;
                        }
                        else
                        {
                            Log.Error("Unable to find " + playerName + " in online players when requesting removal.");
                        }
                    }
                    if (_displayWeaponPopularity && (DateTime.UtcNow - _lastCategoryListing).TotalMinutes > _weaponPopularityDisplayMinutes)
                    {
                        var loadoutPlayers = _playerDictionary.Values.Where(aPlayer => aPlayer.Loadout != null);
                        if (loadoutPlayers.Any())
                        {
                            var loadoutPlayers1 = loadoutPlayers.Where(aPlayer => aPlayer.Team == 1);
                            var loadoutPlayers2 = loadoutPlayers.Where(aPlayer => aPlayer.Team == 2);

                            var highestCategory1 = loadoutPlayers1
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.CategoryReadable)
                                .Select(listing => new
                                {
                                    weaponCategory = listing.Key,
                                    Count = listing.Count()
                                })
                                .OrderByDescending(listing => listing.Count)
                                .FirstOrDefault();

                            var highestCategory2 = loadoutPlayers2
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.CategoryReadable)
                                .Select(listing => new
                                {
                                    weaponCategory = listing.Key,
                                    Count = listing.Count()
                                })
                                .OrderByDescending(listing => listing.Count)
                                .FirstOrDefault();

                            var weaponCounts = loadoutPlayers
                                .GroupBy(aPlayer => aPlayer.Loadout.KitItemPrimary.Slug)
                                .Select(listing => new
                                {
                                    weaponSlug = listing.Key,
                                    Count = listing.Count()
                                });
                            var highestCount = weaponCounts.Max(listing => listing.Count);
                            var highestWeapons = weaponCounts.Where(listing => listing.Count >= highestCount);
                            var highestWeapon = highestWeapons.ElementAt(new Random(Environment.TickCount).Next(highestWeapons.Count()));

                            _lastCategoryListing = DateTime.UtcNow;
                            if (highestWeapon != null && highestCategory1 != null && highestCategory2 != null)
                            {
                                String message = "US " + highestCategory1.weaponCategory.ToLower() + " " + Math.Round((Double)highestCategory1.Count / (Double)loadoutPlayers1.Count() * 100.0) + "% / RU " + highestCategory2.weaponCategory.ToLower() + " " + Math.Round((Double)highestCategory2.Count / (Double)loadoutPlayers2.Count() * 100.0) + "% / Top Weap: " + highestWeapon.weaponSlug + ", " + highestWeapon.Count + " players";
                                AdminSayMessage(message);
                                Log.Info(message);
                            }
                        }
                    }
                }
                _firstPlayerListComplete = true;
                _playerProcessingWaitHandle.Set();
            }
            catch (Exception e)
            {
                Log.Exception("Error while receiving online soldiers.", e);
            }
            Log.Debug("ReceiveOnlineSoldiers finished!", 6);
        }

        public void AdminSayMessage(String message)
        {
            AdminSayMessage(message, true);
        }

        public void AdminSayMessage(String message, Boolean displayProconChat)
        {
            Log.Debug("Entering adminSay", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminSay");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + message);
                }
                string[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                foreach (String line in lineSplit)
                {
                    ExecuteCommand("procon.protected.send", "admin.say", line, "all");
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin say.", e);
            }
            Log.Debug("Exiting adminSay", 7);
        }

        public void PlayerSayMessage(String target, String message)
        {
            PlayerSayMessage(target, message, true, 1);
        }

        public void PlayerSayMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug("Entering playerSayMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(target) || String.IsNullOrEmpty(message))
                {
                    Log.Error("target or message null in playerSayMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Say > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++)
                {
                    string[] lineSplit = message.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (String line in lineSplit)
                    {
                        ExecuteCommand("procon.protected.send", "admin.say", line, "player", target);
                    }
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending message to player.", e);
            }
            Log.Debug("Exiting playerSayMessage", 7);
        }

        public void AdminYellMessage(String message)
        {
            AdminYellMessage(message, true);
        }

        public void AdminYellMessage(String message, Boolean displayProconChat)
        {
            Log.Debug("Entering adminYell", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in adminYell");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + YellDuration + "s] > " + message);
                }
                ExecuteCommand("procon.protected.send", "admin.yell", message.ToUpper(), YellDuration + "", "all");
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin yell.", e);
            }
            Log.Debug("Exiting adminYell", 7);
        }

        public void PlayerYellMessage(String target, String message)
        {
            PlayerYellMessage(target, message, true, 1);
        }

        public void PlayerYellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            Log.Debug("Entering PlayerYellMessage", 7);
            try
            {
                if (String.IsNullOrEmpty(message))
                {
                    Log.Error("message null in PlayerYellMessage");
                    return;
                }
                if (displayProconChat)
                {
                    ProconChatWrite("Yell[" + YellDuration + "s] > " + target + " > " + message);
                }
                for (int count = 0; count < spamCount; count++)
                {
                    ExecuteCommand("procon.protected.send", "admin.yell", ((_gameVersion == GameVersion.BF4) ? (Environment.NewLine) : ("")) + message.ToUpper(), YellDuration + "", "player", target);
                    _threadMasterWaitHandle.WaitOne(50);
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while sending admin yell.", e);
            }
            Log.Debug("Exiting PlayerYellMessage", 7);
        }

        public void AdminTellMessage(String message)
        {
            AdminTellMessage(message, true);
        }

        public void AdminTellMessage(String message, Boolean displayProconChat)
        {
            if (displayProconChat)
            {
                ProconChatWrite("Tell[" + YellDuration + "s] > " + message);
            }
            AdminSayMessage(message, false);
            AdminYellMessage(message, false);
        }

        public void PlayerTellMessage(String target, String message)
        {
            PlayerTellMessage(target, message, true, 1);
        }

        public void PlayerTellMessage(String target, String message, Boolean displayProconChat, Int32 spamCount)
        {
            if (displayProconChat)
            {
                ProconChatWrite("Tell[" + YellDuration + "s] > " + target + " > " + message);
            }
            PlayerSayMessage(target, message, false, spamCount);
            PlayerYellMessage(target, message, false, spamCount);
        }

        public Boolean LoadWarsawLibrary()
        {
            Log.Debug("Entering LoadWarsawLibrary", 7);
            try
            {
                if (_gameVersion == GameVersion.BF4)
                {
                    var library = new WarsawLibrary();
                    Log.Info("Downloading WARSAW library.");
                    Hashtable responseData = FetchWarsawLibrary();

                    //Response data
                    if (responseData == null)
                    {
                        Log.Error("WARSAW library fetch failed, unable to generate library.");
                        return false;
                    }
                    //Compact element
                    if (!responseData.ContainsKey("compact"))
                    {
                        Log.Error("WARSAW library fetch did not contain 'compact' element, unable to generate library.");
                        return false;
                    }
                    var compact = (Hashtable)responseData["compact"];
                    if (compact == null)
                    {
                        Log.Error("Compact section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapons element
                    if (!compact.ContainsKey("weapons"))
                    {
                        Log.Error("Warsaw compact section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var compactWeapons = (Hashtable)compact["weapons"];
                    if (compactWeapons == null)
                    {
                        Log.Error("Compact weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact weapon accessory element
                    if (!compact.ContainsKey("weaponaccessory"))
                    {
                        Log.Error("Warsaw compact section did not contain 'weaponaccessory' element, unable to generate library.");
                        return false;
                    }
                    var compactWeaponAccessory = (Hashtable)compact["weaponaccessory"];
                    if (compactWeaponAccessory == null)
                    {
                        Log.Error("Weapon accessory section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicles element
                    if (!compact.ContainsKey("vehicles"))
                    {
                        Log.Error("Warsaw compact section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicles = (Hashtable)compact["vehicles"];
                    if (compactVehicles == null)
                    {
                        Log.Error("Compact vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact kit items element
                    if (!compact.ContainsKey("kititems"))
                    {
                        Log.Error("Warsaw compact section did not contain 'kititems' element, unable to generate library.");
                        return false;
                    }
                    var compactKitItems = (Hashtable)compact["kititems"];
                    if (compactKitItems == null)
                    {
                        Log.Error("Kit items section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Compact vehicle unlocks element
                    if (!compact.ContainsKey("vehicleunlocks"))
                    {
                        Log.Error("Warsaw compact section did not contain 'vehicleunlocks' element, unable to generate library.");
                        return false;
                    }
                    var compactVehicleUnlocks = (Hashtable)compact["vehicleunlocks"];
                    if (compactVehicleUnlocks == null)
                    {
                        Log.Error("Vehicle unlocks section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout element
                    if (!responseData.ContainsKey("loadout"))
                    {
                        Log.Error("WARSAW library fetch did not contain 'loadout' element, unable to generate library.");
                        return false;
                    }
                    var loadout = (Hashtable)responseData["loadout"];
                    if (loadout == null)
                    {
                        Log.Error("Loadout section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout weapons element
                    if (!loadout.ContainsKey("weapons"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'weapons' element, unable to generate library.");
                        return false;
                    }
                    var loadoutWeapons = (Hashtable)loadout["weapons"];
                    if (loadoutWeapons == null)
                    {
                        Log.Error("Loadout weapons section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout kits element
                    if (!loadout.ContainsKey("kits"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'kits' element, unable to generate library.");
                        return false;
                    }
                    var loadoutKits = (ArrayList)loadout["kits"];
                    if (loadoutKits == null)
                    {
                        Log.Error("Loadout kits section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }
                    //Loadout vehicles element
                    if (!loadout.ContainsKey("vehicles"))
                    {
                        Log.Error("Warsaw loadout section did not contain 'vehicles' element, unable to generate library.");
                        return false;
                    }
                    var loadoutVehicles = (ArrayList)loadout["vehicles"];
                    if (loadoutVehicles == null)
                    {
                        Log.Error("Loadout vehicles section of WARSAW library failed parse, unable to generate library.");
                        return false;
                    }

                    library.Items.Clear();
                    foreach (DictionaryEntry entry in compactWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var item = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        if (_displayLoadoutDebug)
                        {
                            Log.Info("Loading debug warsaw ID " + item.WarsawID);
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        item.Category = (String)weaponData["category"];
                        if (String.IsNullOrEmpty(item.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        item.CategoryReadable = item.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        item.Name = (String)weaponData["name"];
                        if (String.IsNullOrEmpty(item.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        item.Name = item.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab categoryType------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        item.CategoryTypeReadable = (String)weaponData["categoryType"];
                        if (String.IsNullOrEmpty(item.CategoryTypeReadable))
                        {
                            item.CategoryTypeReadable = "General";
                        }
                        //Parsed categoryType does not require any modifications

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        item.Slug = (String)weaponData["slug"];
                        if (String.IsNullOrEmpty(item.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        item.Slug = item.Slug.Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.Items[item.WarsawID] = item;
                    }

                    foreach (DictionaryEntry entry in compactKitItems)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var kitItem = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        if (_displayLoadoutDebug)
                        {
                            Log.Info("Loading debug warsaw ID " + kitItem.WarsawID);
                        }

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        kitItem.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(kitItem.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        kitItem.CategoryReadable = kitItem.Category.Split('_').Last().Replace('_', ' ').ToUpper();
                        if (kitItem.CategoryReadable != "GADGET" && kitItem.CategoryReadable != "GRENADE")
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'category' not gadget or grenade.");
                            continue;
                        }

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        kitItem.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(kitItem.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        kitItem.Name = kitItem.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        kitItem.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(kitItem.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting kit item '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        kitItem.Slug = kitItem.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        if (String.IsNullOrEmpty(kitItem.CategoryTypeReadable))
                        {
                            kitItem.CategoryTypeReadable = "General";
                        }

                        //Assign the item
                        if (!library.Items.ContainsKey(kitItem.WarsawID))
                        {
                            library.Items[kitItem.WarsawID] = kitItem;
                        }
                    }

                    library.ItemAccessories.Clear();
                    foreach (DictionaryEntry entry in compactWeaponAccessory)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var itemAccessory = new WarsawItemAccessory
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        //Grab the contents
                        var weaponAccessoryData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        itemAccessory.Category = (String)weaponAccessoryData["category"];
                        if (String.IsNullOrEmpty(itemAccessory.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        itemAccessory.CategoryReadable = itemAccessory.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        itemAccessory.Name = (String)weaponAccessoryData["name"];
                        if (String.IsNullOrEmpty(itemAccessory.Name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        itemAccessory.Name = itemAccessory.Name.TrimStart("WARSAW_ID_P_INAME_".ToCharArray()).TrimStart("WARSAW_ID_P_WNAME_".ToCharArray()).TrimStart("WARSAW_ID_P_ANAME_".ToCharArray()).Replace('_', ' ').ToLower();

                        //Grab slug------------------------------------------------------------------------------
                        if (!weaponAccessoryData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        itemAccessory.Slug = (String)weaponAccessoryData["slug"];
                        if (String.IsNullOrEmpty(itemAccessory.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting weapon accessory '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        itemAccessory.Slug = itemAccessory.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.ItemAccessories[itemAccessory.WarsawID] = itemAccessory;
                    }

                    library.Vehicles.Clear();
                    foreach (DictionaryEntry entry in compactVehicles)
                    {
                        String category = (String)entry.Key;
                        if (!category.StartsWith("WARSAW_ID"))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                Log.Info("Rejecting vehicle element '" + entry.Key + "', key not a valid ID.");
                            continue;
                        }

                        var vehicle = new WarsawVehicle
                        {
                            Category = category
                        };
                        vehicle.CategoryReadable = vehicle.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab the contents
                        var vehicleData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleData.ContainsKey("categoryType"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle '" + category + "'. Element did not contain 'categoryType'.");
                            continue;
                        }
                        vehicle.CategoryType = (String)vehicleData["categoryType"];
                        if (String.IsNullOrEmpty(vehicle.CategoryType))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle '" + category + "'. 'categoryType' was invalid.");
                            continue;
                        }
                        vehicle.CategoryTypeReadable = vehicle.CategoryType;

                        //Assign the linked RCON codes
                        switch (vehicle.Category)
                        {
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/M1A2/M1Abrams");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/M1A2/spec/M1Abrams_Night");
                                vehicle.LinkedRCONCodes.Add("T90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_MBT_Type99/CH_MBT_Type99");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/BTR-90/BTR90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/BTR-90/spec/BTR90_Night");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_IFV_ZBD09/CH_IFV_ZBD09");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/LAV25");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/spec/LAV25_Night");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEAA":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_AA_PGZ-95/CH_AA_PGZ-95");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/LAV25/LAV_AD");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/9K22_Tunguska_M/9K22_Tunguska_M");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKBOAT":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/US_FAC-CB90/US_FAC-CB90");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_FAC_DV15/CH_FAC_DV15");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_FAC_DV15/spec/CH_FAC_DV15_RU");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESTEALTHJET":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/F35/F35B");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Ch_FJET_J-20/CH_FJET_J-20");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/RU_FJET_T-50_Pak_FA/RU_FJET_T-50_Pak_FA");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESCOUTHELI":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/AH6/AH6_Littlebird");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Z11W/Z-11w");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Z11W/spec/Z-11w_CH");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKHELI":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/AH1Z/AH1Z");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/Mi28/Mi28");
                                vehicle.LinkedRCONCodes.Add("Z-10w");
                                break;
                            case "WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKJET":
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/A-10_THUNDERBOLT/A10_THUNDERBOLT");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/SU-25TM/SU-25TM");
                                vehicle.LinkedRCONCodes.Add("Gameplay/Vehicles/CH_JET_Qiang-5-fantan/CH_JET_Q5_FANTAN");
                                break;
                            default:
                                continue;
                        }

                        //Assign the vehicle
                        library.Vehicles[vehicle.Category] = vehicle;
                        if (_displayLoadoutDebug)
                            Log.Success("Vehicle " + vehicle.Category + " added. " + library.Vehicles.ContainsKey(vehicle.Category));
                    }

                    library.VehicleUnlocks.Clear();
                    foreach (DictionaryEntry entry in compactVehicleUnlocks)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }
                        var vehicleUnlock = new WarsawItem
                        {
                            WarsawID = warsawID.ToString(CultureInfo.InvariantCulture)
                        };

                        //Grab the contents
                        var vehicleUnlockData = (Hashtable)entry.Value;
                        //Grab category------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("category"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'category'.");
                            continue;
                        }
                        vehicleUnlock.Category = (String)vehicleUnlockData["category"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Category))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'category' was invalid.");
                            continue;
                        }
                        vehicleUnlock.CategoryReadable = vehicleUnlock.Category.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab name------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("name"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock'" + warsawID + "'. Element did not contain 'name'.");
                            continue;
                        }
                        var name = (String)vehicleUnlockData["name"];
                        if (String.IsNullOrEmpty(name))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'name' was invalid.");
                            continue;
                        }
                        //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
                        name = name.Split('_').Last().Replace('_', ' ').ToUpper();

                        //Grab slug------------------------------------------------------------------------------
                        if (!vehicleUnlockData.ContainsKey("slug"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. Element did not contain 'slug'.");
                            continue;
                        }
                        vehicleUnlock.Slug = (String)vehicleUnlockData["slug"];
                        if (String.IsNullOrEmpty(vehicleUnlock.Slug))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting vehicle unlock '" + warsawID + "'. 'slug' was invalid.");
                            continue;
                        }
                        //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
                        vehicleUnlock.Slug = vehicleUnlock.Slug.TrimEnd('1').TrimEnd('2').TrimEnd('2').TrimEnd('3').TrimEnd('4').TrimEnd('5').Replace('_', ' ').Replace('-', ' ').ToUpper();

                        //Assign the weapon
                        library.VehicleUnlocks[vehicleUnlock.WarsawID] = vehicleUnlock;
                    }

                    //Fill allowed accessories for each weapon
                    foreach (DictionaryEntry entry in loadoutWeapons)
                    {
                        //Try to parse the entry key as an integer, only accept the entry on success
                        Int64 warsawID;
                        if (!Int64.TryParse((String)entry.Key, out warsawID))
                        {
                            //Reject the entry
                            continue;
                        }

                        WarsawItem weapon;
                        if (!library.Items.TryGetValue(warsawID.ToString(CultureInfo.InvariantCulture), out weapon))
                        {
                            //Reject the entry
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting loadout weapon element '" + warsawID + "', ID not found in weapon library.");
                            continue;
                        }

                        //Grab the contents
                        var weaponData = (Hashtable)entry.Value;
                        if (weaponData == null)
                        {
                            Log.Error("Rejecting loadout weapon element " + warsawID + ", could not parse weapon data.");
                            continue;
                        }
                        //Grab slots------------------------------------------------------------------------------
                        if (!weaponData.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("Rejecting loadout weapon element '" + warsawID + "'. Element did not contain 'slots'.");
                            continue;
                        }
                        var slots = (ArrayList)weaponData["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                Log.Error("Rejecting slot entry for " + warsawID + ", could not parse slot into hashtable.");
                                continue;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                if (_displayLoadoutDebug)
                                    Log.Error("Rejecting slot entry for " + warsawID + ". Element did not contain 'sid'.");
                                continue;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT"))
                            {
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    Log.Error("Rejecting slot entry for " + warsawID + ". Element did not contain 'items'.");
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];
                            Dictionary<String, WarsawItemAccessory> allowedItems;
                            if (weapon.AccessoriesAllowed.ContainsKey(category))
                            {
                                //Existing list, add to it
                                allowedItems = weapon.AccessoriesAllowed[category];
                            }
                            else
                            {
                                //New list, add it
                                allowedItems = new Dictionary<String, WarsawItemAccessory>();
                                weapon.AccessoriesAllowed[category] = allowedItems;
                            }
                            foreach (String accessoryID in items)
                            {
                                //Attempt to fetch accessory from library
                                WarsawItemAccessory accessory;
                                if (library.ItemAccessories.TryGetValue(accessoryID, out accessory))
                                {
                                    allowedItems[accessoryID] = accessory;
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        Log.Error("Rejecting allowed accessory entry for " + accessoryID + ". Accessory not found in library.");
                                }
                            }
                        }
                    }

                    //Fill allowed items for each class
                    foreach (Hashtable entry in loadoutKits)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            Log.Error("Kit entry did not contain 'sid' element, unable to generate library.");
                            return false;
                        }
                        var kitKey = (String)entry["sid"];

                        WarsawKit kit;
                        switch (kitKey)
                        {
                            case "WARSAW_ID_M_ASSAULT":
                                kit = library.KitAssault;
                                break;
                            case "WARSAW_ID_M_ENGINEER":
                                kit = library.KitEngineer;
                                break;
                            case "WARSAW_ID_M_SUPPORT":
                                kit = library.KitSupport;
                                break;
                            case "WARSAW_ID_M_RECON":
                                kit = library.KitRecon;
                                break;
                            default:
                                Log.Error("Kit entry could not be assigned to a valid kit type, unable to generate library.");
                                return false;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            Log.Error("Kit entry '" + kitKey + "' did not contain 'slots' element, unable to generate library.");
                            return false;
                        }
                        var slots = (ArrayList)entry["slots"];
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                Log.Error("Slot entry for kit '" + kitKey + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                Log.Error("Slot entry for kit '" + kitKey + "', did not contain 'sid' element, unable to generate library.");
                                return false;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT"))
                            {
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    Log.Error("Rejecting slot entry '" + category + "' for class '" + kitKey + "', element did not contain 'items'.");
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];

                            //Decide which structure is being filled for this slot
                            Dictionary<String, WarsawItem> allowedItems;
                            switch (category)
                            {
                                case "WARSAW_ID_M_SOLDIER_PRIMARY":
                                    allowedItems = kit.KitAllowedPrimary;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_SECONDARY":
                                    allowedItems = kit.KitAllowedSecondary;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GADGET1":
                                    allowedItems = kit.KitAllowedGadget1;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GADGET2":
                                    allowedItems = kit.KitAllowedGadget2;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_GRENADES":
                                    allowedItems = kit.KitAllowedGrenades;
                                    break;
                                case "WARSAW_ID_M_SOLDIER_KNIFE":
                                    allowedItems = kit.KitAllowedKnife;
                                    break;
                                default:
                                    if (_displayLoadoutDebug)
                                        Log.Info("Rejecting slot item entry '" + category + "' for class '" + kitKey + "'.");
                                    continue;
                            }

                            foreach (String itemID in items)
                            {
                                //Attempt to fetch item from library
                                WarsawItem item;
                                if (library.Items.TryGetValue(itemID, out item))
                                {
                                    allowedItems[itemID] = item;
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        Log.Error("Rejecting allowed item entry " + itemID + ". Item not found in library.");
                                }
                            }
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(kit.KitType + " parsed. Allowed: " + kit.KitAllowedPrimary.Count + " primary weapons, " + kit.KitAllowedSecondary.Count + " secondary weapons, " + kit.KitAllowedGadget1.Count + " primary gadgets, " + kit.KitAllowedGadget2.Count + " secondary gadgets, " + kit.KitAllowedGrenades.Count + " grenades, and " + kit.KitAllowedKnife.Count + " knives.");
                    }

                    //Fill allowed items for each vehicle
                    foreach (Hashtable entry in loadoutVehicles)
                    {
                        //Get the kit key
                        if (!entry.ContainsKey("sid"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry did not contain 'sid' element, skipping.");
                            continue;
                        }
                        var vehicleCategory = (String)entry["sid"];

                        //Reject all non-EOR entries
                        if (!vehicleCategory.Contains("WARSAW_ID_EOR"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry was not an EOR entry, skipping.");
                            continue;
                        }

                        WarsawVehicle vehicle;
                        if (!library.Vehicles.TryGetValue(vehicleCategory, out vehicle))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle category " + vehicleCategory + " not found, skipping.");
                            continue;
                        }

                        //Grab slots------------------------------------------------------------------------------
                        if (!entry.ContainsKey("slots"))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Vehicle entry '" + vehicleCategory + "' did not contain 'slots' element, skipping.");
                            continue;
                        }
                        var slots = (ArrayList)entry["slots"];
                        Int32 slotIndex = 0;
                        foreach (Object slotEntry in slots)
                        {
                            //Grab the contents
                            var slotTable = (Hashtable)slotEntry;
                            if (slotTable == null)
                            {
                                Log.Error("Slot entry for vehicle '" + vehicleCategory + "', could not parse slot into hashtable, unable to generate library.");
                                return false;
                            }
                            //Grab category------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("sid"))
                            {
                                Log.Error("Slot entry for vehicle '" + vehicleCategory + "', did not contain 'sid' element, unable to generate library.");
                                return false;
                            }
                            var category = (String)slotTable["sid"];
                            //Reject all paint categories
                            if (category.Contains("PAINT"))
                            {
                                slotIndex++;
                                continue;
                            }
                            //Grab items------------------------------------------------------------------------------
                            if (!slotTable.ContainsKey("items"))
                            {
                                if (_displayLoadoutDebug)
                                    Log.Error("Rejecting slot entry '" + category + "' for vehicle '" + vehicleCategory + "', element did not contain 'items'.");
                                slotIndex++;
                                continue;
                            }
                            var items = (ArrayList)slotTable["items"];

                            //Decide which structure is being filled for this slot
                            Dictionary<String, WarsawItem> allowedUnlocks;
                            switch (category)
                            {
                                case "WARSAW_ID_P_CAT_PRIMARY":
                                    vehicle.SlotIndexPrimary = slotIndex;
                                    allowedUnlocks = vehicle.AllowedPrimaries;
                                    break;
                                case "WARSAW_ID_P_CAT_SECONDARY":
                                    vehicle.SlotIndexSecondary = slotIndex;
                                    allowedUnlocks = vehicle.AllowedSecondaries;
                                    break;
                                case "WARSAW_ID_P_CAT_COUNTERMEASURE":
                                    vehicle.SlotIndexCountermeasure = slotIndex;
                                    allowedUnlocks = vehicle.AllowedCountermeasures;
                                    break;
                                case "WARSAW_ID_P_CAT_SIMPLE_OPTICS":
                                    vehicle.SlotIndexOptic = slotIndex;
                                    allowedUnlocks = vehicle.AllowedOptics;
                                    break;
                                case "WARSAW_ID_P_CAT_UPGRADES":
                                case "WARSAW_ID_P_CAT_UPGRADE":
                                    vehicle.SlotIndexUpgrade = slotIndex;
                                    allowedUnlocks = vehicle.AllowedUpgrades;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_SECONDARY":
                                    vehicle.SlotIndexSecondaryGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedSecondariesGunner;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_OPTICS":
                                    vehicle.SlotIndexOpticGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedOpticsGunner;
                                    break;
                                case "WARSAW_ID_P_CAT_GUNNER_UPGRADE":
                                    vehicle.SlotIndexUpgradeGunner = slotIndex;
                                    allowedUnlocks = vehicle.AllowedUpgradesGunner;
                                    break;
                                default:
                                    if (_displayLoadoutDebug)
                                        Log.Info("Rejecting slot item entry '" + category + "' for vehicle '" + vehicleCategory + "'.");
                                    slotIndex++;
                                    continue;
                            }

                            foreach (String unlockID in items)
                            {
                                //Attempt to fetch item from library
                                WarsawItem item;
                                if (library.VehicleUnlocks.TryGetValue(unlockID, out item))
                                {
                                    allowedUnlocks[unlockID] = item;
                                    //Assign the vehicle
                                    if (item.AssignedVehicle == null)
                                    {
                                        item.AssignedVehicle = vehicle;
                                    }
                                    else
                                    {
                                        Log.Warn(unlockID + " already assigned to a vehicle, " + item.AssignedVehicle.CategoryType);
                                    }
                                }
                                else
                                {
                                    if (_displayLoadoutDebug)
                                        Log.Error("Rejecting allowed unlock entry " + unlockID + ". Item not found in library.");
                                }
                            }
                            slotIndex++;
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(vehicle.CategoryType + " parsed. Allowed: " +
                                vehicle.AllowedPrimaries.Count + " primary weapons, " +
                                vehicle.AllowedSecondaries.Count + " secondary weapons, " +
                                vehicle.AllowedCountermeasures.Count + " countermeasures, " +
                                vehicle.AllowedOptics.Count + " optics, " +
                                vehicle.AllowedUpgrades.Count + " upgrades, " +
                                vehicle.AllowedSecondariesGunner.Count + " gunner secondary weapons, " +
                                vehicle.AllowedOpticsGunner.Count + " gunner optics, and " +
                                vehicle.AllowedUpgradesGunner.Count + " gunner upgrades. ");
                    }

                    _warsawLibrary = library;
                    _warsawLibraryLoaded = true;
                    UpdateSettingPage();
                    return true;
                }
                Log.Error("Game not BF4, unable to process WARSAW library.");
                return false;
            }
            catch (Exception e)
            {
                Log.Exception("Error while parsing WARSAW library.", e);
            }
            Log.Debug("Exiting LoadWarsawLibrary", 7);
            return false;
        }

        private Hashtable FetchWarsawLibrary()
        {
            Hashtable library = null;
            try
            {
                String response;
                try
                {
                    response = "https://raw.githubusercontent.com/AdKats/AdKats/master/lib/WarsawCodeBook.json"
                        .GetStringAsync().Result;
                }
                catch (Exception)
                {
                    try
                    {
                        response = "http://api.gamerethos.net/adkats/fetch/warsaw"
                            .GetStringAsync().Result;
                    }
                    catch (Exception e)
                    {
                        Log.Exception("Error while downloading raw WARSAW library.", e);
                        return null;
                    }
                }
                library = (Hashtable)JSON.JsonDecode(response);
            }
            catch (Exception e)
            {
                Log.Exception("Unexpected error while fetching WARSAW library", e);
                return null;
            }
            return library;
        }

        private AdKatsLoadout GetPlayerLoadout(String personaID)
        {
            Log.Debug("Entering GetPlayerLoadout", 7);
            try
            {
                if (_gameVersion == GameVersion.BF4)
                {
                    var loadout = new AdKatsLoadout();
                    Hashtable responseData = FetchPlayerLoadout(personaID);
                    if (responseData == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout fetch failed, unable to parse player loadout.");
                        return null;
                    }
                    if (!responseData.ContainsKey("data"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout fetch did not contain 'data' element, unable to parse player loadout.");
                        return null;
                    }
                    var data = (Hashtable)responseData["data"];
                    if (data == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout failed parse, unable to parse player loadout.");
                        return null;
                    }
                    //Get parsed back persona ID
                    if (!data.ContainsKey("personaId"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'personaId' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.PersonaID = data["personaId"].ToString();
                    //Get persona name
                    if (!data.ContainsKey("personaName"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'personaName' element, unable to parse player loadout.");
                        return null;
                    }
                    loadout.Name = data["personaName"].ToString();
                    //Get weapons and their attachements
                    if (!data.ContainsKey("currentLoadout"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Data section of loadout did not contain 'currentLoadout' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutHashtable = (Hashtable)data["currentLoadout"];
                    if (currentLoadoutHashtable == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("weapons"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section did not contain 'weapons' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutWeapons = (Hashtable)currentLoadoutHashtable["weapons"];
                    if (currentLoadoutWeapons == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Weapon loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    if (!currentLoadoutHashtable.ContainsKey("vehicles"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section did not contain 'vehicles' element, unable to parse player loadout.");
                        return null;
                    }
                    var currentLoadoutVehicles = (ArrayList)currentLoadoutHashtable["vehicles"];
                    if (currentLoadoutVehicles == null)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Vehicles loadout section failed parse, unable to parse player loadout.");
                        return null;
                    }
                    foreach (DictionaryEntry weaponEntry in currentLoadoutWeapons)
                    {
                        if (weaponEntry.Key.ToString() != "0")
                        {
                            WarsawItem warsawItem;
                            if (_warsawLibrary.Items.TryGetValue(weaponEntry.Key.ToString(), out warsawItem))
                            {
                                //Create new instance of the weapon for this player
                                var loadoutItem = new WarsawItem()
                                {
                                    WarsawID = warsawItem.WarsawID,
                                    CategoryReadable = warsawItem.CategoryReadable,
                                    CategoryTypeReadable = warsawItem.CategoryTypeReadable,
                                    Name = warsawItem.Name,
                                    Slug = warsawItem.Slug
                                };
                                foreach (String accessoryID in (ArrayList)weaponEntry.Value)
                                {
                                    if (accessoryID != "0")
                                    {
                                        WarsawItemAccessory warsawItemAccessory;
                                        if (_warsawLibrary.ItemAccessories.TryGetValue(accessoryID, out warsawItemAccessory))
                                        {
                                            loadoutItem.AccessoriesAssigned[warsawItemAccessory.WarsawID] = warsawItemAccessory;
                                        }
                                    }
                                }
                                loadout.LoadoutItems[loadoutItem.WarsawID] = loadoutItem;
                            }
                        }
                    }

                    //Parse vehicles
                    for (Int32 index = 0; index < currentLoadoutVehicles.Count; index++)
                    {
                        WarsawVehicle libraryVehicle;
                        switch (index)
                        {
                            case 0:
                                //MBT
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEMBT", out libraryVehicle))
                                {
                                    Log.Error("Failed to fetch MBT vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 1:
                                //IFV
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEIFV", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch IFV vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 2:
                                //AA
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEAA", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AA vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 3:
                                //Boat
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKBOAT", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Boat vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 4:
                                //Stealth
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESTEALTHJET", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Stealth vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 5:
                                //Scout
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLESCOUTHELI", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch Scout vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 6:
                                //AttkHeli
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKHELI", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AttkHeli vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            case 7:
                                //AttkJet
                                if (!_warsawLibrary.Vehicles.TryGetValue("WARSAW_ID_EOR_SCORINGBUCKET_VEHICLEATTACKJET", out libraryVehicle))
                                {
                                    Log.Info("Failed to fetch AttkJet vehicle, unable to parse player loadout.");
                                    return null;
                                }
                                break;
                            default:
                                continue;
                        }
                        //Duplicate the vehicle
                        var vehicle = new WarsawVehicle()
                        {
                            Category = libraryVehicle.Category,
                            CategoryReadable = libraryVehicle.CategoryReadable,
                            CategoryType = libraryVehicle.CategoryType,
                            CategoryTypeReadable = libraryVehicle.CategoryTypeReadable,
                            LinkedRCONCodes = libraryVehicle.LinkedRCONCodes
                        };
                        //Fetch the vehicle items
                        var vehicleItems = (ArrayList)currentLoadoutVehicles[index];
                        //Assign the primary
                        if (libraryVehicle.SlotIndexPrimary >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexPrimary];
                            if (!libraryVehicle.AllowedPrimaries.TryGetValue(itemID, out vehicle.AssignedPrimary))
                            {
                                var defaultItem = libraryVehicle.AllowedPrimaries.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle primary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedPrimary = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedPrimary.WarsawID] = vehicle.AssignedPrimary;
                        }
                        //Assign the secondary
                        if (libraryVehicle.SlotIndexSecondary >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexSecondary];
                            if (!libraryVehicle.AllowedSecondaries.TryGetValue(itemID, out vehicle.AssignedSecondary))
                            {
                                var defaultItem = libraryVehicle.AllowedSecondaries.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedSecondary = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedSecondary.WarsawID] = vehicle.AssignedSecondary;
                        }
                        //Assign the countermeasure
                        if (libraryVehicle.SlotIndexCountermeasure >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexCountermeasure];
                            if (!libraryVehicle.AllowedCountermeasures.TryGetValue(itemID, out vehicle.AssignedCountermeasure))
                            {
                                var defaultItem = libraryVehicle.AllowedCountermeasures.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle countermeasure " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedCountermeasure = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedCountermeasure.WarsawID] = vehicle.AssignedCountermeasure;
                        }
                        //Assign the optic
                        if (libraryVehicle.SlotIndexOptic >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexOptic];
                            if (!libraryVehicle.AllowedOptics.TryGetValue(itemID, out vehicle.AssignedOptic))
                            {
                                var defaultItem = libraryVehicle.AllowedOptics.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedOptic = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedOptic.WarsawID] = vehicle.AssignedOptic;
                        }
                        //Assign the upgrade
                        if (libraryVehicle.SlotIndexUpgrade >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexUpgrade];
                            if (!libraryVehicle.AllowedUpgrades.TryGetValue(itemID, out vehicle.AssignedUpgrade))
                            {
                                var defaultItem = libraryVehicle.AllowedUpgrades.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedUpgrade = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedUpgrade.WarsawID] = vehicle.AssignedUpgrade;
                        }
                        //Assign the gunner secondary
                        if (libraryVehicle.SlotIndexSecondaryGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexSecondaryGunner];
                            if (!libraryVehicle.AllowedSecondariesGunner.TryGetValue(itemID, out vehicle.AssignedSecondaryGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedSecondariesGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle gunner secondary " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedSecondaryGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedSecondaryGunner.WarsawID] = vehicle.AssignedSecondaryGunner;
                        }
                        //Assign the gunner optic
                        if (libraryVehicle.SlotIndexOpticGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexOpticGunner];
                            if (!libraryVehicle.AllowedOpticsGunner.TryGetValue(itemID, out vehicle.AssignedOpticGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedOpticsGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle gunner optic " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedOpticGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedOpticGunner.WarsawID] = vehicle.AssignedOpticGunner;
                        }
                        //Assign the gunner upgrade
                        if (libraryVehicle.SlotIndexUpgradeGunner >= 0)
                        {
                            var itemID = (String)vehicleItems[libraryVehicle.SlotIndexUpgradeGunner];
                            if (!libraryVehicle.AllowedUpgradesGunner.TryGetValue(itemID, out vehicle.AssignedUpgradeGunner))
                            {
                                var defaultItem = libraryVehicle.AllowedUpgradesGunner.Values.First();
                                if (_displayLoadoutDebug)
                                    Log.Warn("Unable to fetch valid vehicle gunner upgrade " + itemID + " for " + vehicle.Category + ", defaulting to " + defaultItem.Slug + ".");
                                vehicle.AssignedUpgradeGunner = defaultItem;
                            }
                            loadout.VehicleItems[vehicle.AssignedUpgradeGunner.WarsawID] = vehicle.AssignedUpgradeGunner;
                        }
                        loadout.LoadoutVehicles[vehicle.Category] = vehicle;
                        foreach (String rconCode in vehicle.LinkedRCONCodes)
                        {
                            loadout.LoadoutRCONVehicles[rconCode] = vehicle;
                        }
                        if (_displayLoadoutDebug)
                            Log.Info(loadout.Name + ": " +
                                vehicle.CategoryType + ": " +
                                ((vehicle.AssignedPrimary == null) ? ("No Primary") : (vehicle.AssignedPrimary.Slug)) + ", " +
                                ((vehicle.AssignedSecondary == null) ? ("No Secondary") : (vehicle.AssignedSecondary.Slug)) + ", " +
                                ((vehicle.AssignedCountermeasure == null) ? ("No Countermeasure") : (vehicle.AssignedCountermeasure.Slug)) + ", " +
                                ((vehicle.AssignedOptic == null) ? ("No Optic") : (vehicle.AssignedOptic.Slug)) + ", " +
                                ((vehicle.AssignedUpgrade == null) ? ("No Upgrade") : (vehicle.AssignedUpgrade.Slug)) + ", " +
                                ((vehicle.AssignedSecondaryGunner == null) ? ("No Gunner Secondary") : (vehicle.AssignedSecondaryGunner.Slug)) + ", " +
                                ((vehicle.AssignedOpticGunner == null) ? ("No Gunner Optic") : (vehicle.AssignedOpticGunner.Slug)) + ", " +
                                ((vehicle.AssignedUpgradeGunner == null) ? ("No Gunner Upgrade") : (vehicle.AssignedUpgradeGunner.Slug)) + ".");
                    }
                    if (!currentLoadoutHashtable.ContainsKey("selectedKit"))
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Current loadout section did not contain 'selectedKit' element, unable to parse player loadout.");
                        return null;
                    }
                    String selectedKit = currentLoadoutHashtable["selectedKit"].ToString();
                    ArrayList currentLoadoutList;
                    switch (selectedKit)
                    {
                        case "0":
                            loadout.SelectedKit = _warsawLibrary.KitAssault;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[0];
                            break;
                        case "1":
                            loadout.SelectedKit = _warsawLibrary.KitEngineer;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[1];
                            break;
                        case "2":
                            loadout.SelectedKit = _warsawLibrary.KitSupport;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[2];
                            break;
                        case "3":
                            loadout.SelectedKit = _warsawLibrary.KitRecon;
                            currentLoadoutList = (ArrayList)((ArrayList)currentLoadoutHashtable["kits"])[3];
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                Log.Error("Unable to parse selected kit " + selectedKit + ", value is unknown. Unable to parse player loadout.");
                            return null;
                    }
                    if (currentLoadoutList.Count < 6)
                    {
                        if (_displayLoadoutDebug)
                            Log.Error("Loadout kit item entry did not contain 6 valid entries. Unable to parse player loadout.");
                        return null;
                    }
                    //Pull the specifics
                    string loadoutPrimaryID = currentLoadoutList[0].ToString();
                    const string defaultAssaultPrimary = "3590299697"; //ak-12
                    const string defaultEngineerPrimary = "2021343793"; //mx4
                    const string defaultSupportPrimary = "3179658801"; //u-100-mk5
                    const string defaultReconPrimary = "3458855537"; //cs-lr4

                    string loadoutSidearmID = currentLoadoutList[1].ToString();
                    const string defaultSidearm = "944904529"; //p226

                    string loadoutGadget1ID = currentLoadoutList[2].ToString();
                    const string defaultGadget1 = "1694579111"; //nogadget1

                    string loadoutGadget2ID = currentLoadoutList[3].ToString();
                    const string defaultGadget2 = "3164552276"; //nogadget2

                    string loadoutGrenadeID = currentLoadoutList[4].ToString();
                    const string defaultGrenade = "2670747868"; //m67-frag

                    string loadoutKnifeID = currentLoadoutList[5].ToString();
                    const string defaultKnife = "3214146841"; //bayonett

                    //PRIMARY
                    WarsawItem loadoutPrimary;
                    String specificDefault;
                    switch (loadout.SelectedKit.KitType)
                    {
                        case WarsawKit.Type.Assault:
                            specificDefault = defaultAssaultPrimary;
                            break;
                        case WarsawKit.Type.Engineer:
                            specificDefault = defaultEngineerPrimary;
                            break;
                        case WarsawKit.Type.Support:
                            specificDefault = defaultSupportPrimary;
                            break;
                        case WarsawKit.Type.Recon:
                            specificDefault = defaultReconPrimary;
                            break;
                        default:
                            if (_displayLoadoutDebug)
                                Log.Error("Specific kit type not set while assigning primary weapon default. Unable to parse player loadout.");
                            return null;
                    }
                    //Attempt to fetch PRIMARY from library
                    if (!loadout.LoadoutItems.TryGetValue(loadoutPrimaryID, out loadoutPrimary))
                    {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm PRIMARY is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedPrimary.ContainsKey(loadoutPrimary.WarsawID))
                    {
                        if (loadout.LoadoutItems.TryGetValue(specificDefault, out loadoutPrimary))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific PRIMARY (" + loadoutPrimaryID + ") for " + loadout.Name + " was not valid for " + loadout.SelectedKit.KitType + " kit. Defaulting to " + loadoutPrimary.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid PRIMARY (" + loadoutPrimaryID + "->" + specificDefault + ") usable for " + loadout.SelectedKit.KitType + " " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemPrimary = loadoutPrimary;

                    //SIDEARM
                    WarsawItem loadoutSidearm;
                    //Attempt to fetch SIDEARM from library
                    if (!loadout.LoadoutItems.TryGetValue(loadoutSidearmID, out loadoutSidearm))
                    {
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific SIDEARM (" + loadoutSidearmID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm SIDEARM is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedSecondary.ContainsKey(loadoutSidearm.WarsawID))
                    {
                        WarsawItem originalItem = loadoutSidearm;
                        if (loadout.LoadoutItems.TryGetValue(defaultSidearm, out loadoutSidearm))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific SIDEARM (" + loadoutSidearmID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a SIDEARM. Defaulting to " + loadoutSidearm.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid SIDEARM (" + loadoutSidearmID + "->" + defaultSidearm + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitItemSidearm = loadoutSidearm;

                    //GADGET1
                    WarsawItem loadoutGadget1;
                    //Attempt to fetch GADGET1 from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGadget1ID, out loadoutGadget1))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET1 (" + loadoutGadget1ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET1 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget1.ContainsKey(loadoutGadget1.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget1;
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget1, out loadoutGadget1))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET1 (" + loadoutGadget1ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget1.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET1 (" + loadoutGadget1ID + "->" + defaultGadget1 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget1 = loadoutGadget1;

                    //GADGET2
                    WarsawItem loadoutGadget2;
                    //Attempt to fetch GADGET2 from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGadget2ID, out loadoutGadget2))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET2 (" + loadoutGadget2ID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GADGET2 is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGadget2.ContainsKey(loadoutGadget2.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGadget2;
                        if (_warsawLibrary.Items.TryGetValue(defaultGadget2, out loadoutGadget2))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GADGET2 (" + loadoutGadget2ID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GADGET. Defaulting to " + loadoutGadget2.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GADGET2 (" + loadoutGadget2ID + "->" + defaultGadget2 + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGadget2 = loadoutGadget2;

                    //GRENADE
                    WarsawItem loadoutGrenade;
                    //Attempt to fetch GRENADE from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutGrenadeID, out loadoutGrenade))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GRENADE (" + loadoutGrenadeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm GRENADE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedGrenades.ContainsKey(loadoutGrenade.WarsawID))
                    {
                        WarsawItem originalItem = loadoutGrenade;
                        if (_warsawLibrary.Items.TryGetValue(defaultGrenade, out loadoutGrenade))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific GRENADE (" + loadoutGrenadeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a GRENADE. Defaulting to " + loadoutGrenade.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid GRENADE (" + loadoutGrenadeID + "->" + defaultGrenade + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitGrenade = loadoutGrenade;

                    //KNIFE
                    WarsawItem loadoutKnife;
                    //Attempt to fetch KNIFE from library
                    if (!_warsawLibrary.Items.TryGetValue(loadoutKnifeID, out loadoutKnife))
                    {
                        if (_warsawLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific KNIFE (" + loadoutKnifeID + ") for " + loadout.Name + " not found. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    //Confirm KNIFE is valid for this kit
                    if (!loadout.SelectedKit.KitAllowedKnife.ContainsKey(loadoutKnife.WarsawID))
                    {
                        WarsawItem originalItem = loadoutKnife;
                        if (_warsawLibrary.Items.TryGetValue(defaultKnife, out loadoutKnife))
                        {
                            if (_displayLoadoutDebug)
                                Log.Warn("Specific KNIFE (" + loadoutKnifeID + ") " + originalItem.Slug + " for " + loadout.Name + " was not a KNIFE. Defaulting to " + loadoutKnife.Slug + ".");
                        }
                        else
                        {
                            if (_displayLoadoutDebug)
                                Log.Error("No valid KNIFE (" + loadoutKnifeID + "->" + defaultKnife + ") usable for " + loadout.Name + ". Unable to parse player loadout.");
                            return null;
                        }
                    }
                    loadout.KitKnife = loadoutKnife;

                    //Fill the kit ID listings
                    if (!loadout.AllKitItemIDs.Contains(loadoutPrimary.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutPrimary.WarsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutPrimary.AccessoriesAssigned.Values)
                    {
                        if (!loadout.AllKitItemIDs.Contains(accessory.WarsawID))
                        {
                            loadout.AllKitItemIDs.Add(accessory.WarsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutSidearm.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutSidearm.WarsawID);
                    }
                    foreach (WarsawItemAccessory accessory in loadoutSidearm.AccessoriesAssigned.Values)
                    {
                        if (!loadout.AllKitItemIDs.Contains(accessory.WarsawID))
                        {
                            loadout.AllKitItemIDs.Add(accessory.WarsawID);
                        }
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget1.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGadget1.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGadget2.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGadget2.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutGrenade.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutGrenade.WarsawID);
                    }
                    if (!loadout.AllKitItemIDs.Contains(loadoutKnife.WarsawID))
                    {
                        loadout.AllKitItemIDs.Add(loadoutKnife.WarsawID);
                    }
                    return loadout;
                }
                Log.Error("Game not BF4, unable to process player loadout.");
                return null;
            }
            catch (Exception e)
            {
                Log.Exception("Error while parsing player loadout.", e);
            }
            Log.Debug("Exiting GetPlayerLoadout", 7);
            return null;
        }

        private Hashtable FetchPlayerLoadout(String personaID)
        {
            Hashtable loadout = null;
            try
            {
                try
                {
                    DoBattlelogWait();
                    String response = ("http://battlelog.battlefield.com/bf4/loadout/get/PLAYER/" + personaID + "/1/?cacherand=" + Environment.TickCount)
                        .GetStringAsync().Result;
                    loadout = (Hashtable)JSON.JsonDecode(response);
                }
                catch (Exception e)
                {
                    if (e is HttpRequestException)
                    {
                        Log.Warn("Issue connecting to battlelog.");
                        _lastBattlelogAction = DateTime.UtcNow.AddSeconds(30);
                    }
                    else
                    {
                        Log.Exception("Error while loading player loadout.", e);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception("Unexpected error while fetching player loadout.", e);
                return null;
            }
            return loadout;
        }

        public String ExtractString(String s, String tag)
        {
            if (String.IsNullOrEmpty(s) || String.IsNullOrEmpty(tag))
            {
                Log.Error("Unable to extract String. Invalid inputs.");
                return null;
            }
            String startTag = "<" + tag + ">";
            Int32 startIndex = s.IndexOf(startTag, StringComparison.Ordinal) + startTag.Length;
            if (startIndex == -1)
            {
                Log.Error("Unable to extract String. Tag not found.");
            }
            Int32 endIndex = s.IndexOf("</" + tag + ">", startIndex, StringComparison.Ordinal);
            return s.Substring(startIndex, endIndex - startIndex);
        }

        public Boolean SoldierNameValid(String input)
        {
            try
            {
                Log.Debug("Checking player '" + input + "' for validity.", 7);
                if (String.IsNullOrEmpty(input))
                {
                    Log.Debug("Soldier Name empty or null.", 5);
                    return false;
                }
                if (input.Length > 16)
                {
                    Log.Debug("Soldier Name '" + input + "' too long, maximum length is 16 characters.", 5);
                    return false;
                }
                if (new Regex("[^a-zA-Z0-9_-]").Replace(input, "").Length != input.Length)
                {
                    Log.Debug("Soldier Name '" + input + "' contained invalid characters.", 5);
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                //Soldier id caused exception in the regex, definitely not valid
                Log.Error("Soldier Name '" + input + "' contained invalid characters.");
                return false;
            }
        }

        public String FormatTimeString(TimeSpan timeSpan, Int32 maxComponents)
        {
            Log.Debug("Entering formatTimeString", 7);
            String timeString = null;
            if (maxComponents < 1)
            {
                return null;
            }
            try
            {
                String formattedTime = (timeSpan.TotalMilliseconds >= 0) ? ("") : ("-");

                Double secondSubset = Math.Abs(timeSpan.TotalSeconds);
                if (secondSubset < 1)
                {
                    return "0s";
                }
                Double minuteSubset = (secondSubset / 60);
                Double hourSubset = (minuteSubset / 60);
                Double daySubset = (hourSubset / 24);
                Double weekSubset = (daySubset / 7);
                Double monthSubset = (weekSubset / 4);
                Double yearSubset = (monthSubset / 12);

                var years = (Int32)yearSubset;
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
                timeString = formattedTime;
            }
            catch (Exception e)
            {
                Log.Exception("Error while formatting time String.", e);
            }
            if (String.IsNullOrEmpty(timeString))
            {
                timeString = "0s";
            }
            Log.Debug("Exiting formatTimeString", 7);
            return timeString;
        }

        protected void LogThreadExit()
        {
            lock (_aliveThreads)
            {
                _aliveThreads.Remove(Thread.CurrentThread.ManagedThreadId);
            }
        }

        protected void StartAndLogThread(Thread aThread)
        {
            aThread.Start();
            lock (_aliveThreads)
            {
                if (!_aliveThreads.ContainsKey(aThread.ManagedThreadId))
                {
                    _aliveThreads.Add(aThread.ManagedThreadId, aThread);
                    _threadMasterWaitHandle.WaitOne(100);
                }
            }
        }

        public Boolean AdminsOnline()
        {
            return _playerDictionary.Values.Any(aPlayer => aPlayer.IsAdmin);
        }

        public Boolean OnlineAdminSayMessage(String message)
        {
            ProconChatWrite(Log.CMaroon(Log.FBold(message)));
            Boolean adminsTold = false;
            foreach (var aPlayer in _playerDictionary.Values.ToList().Where(aPlayer => aPlayer.IsAdmin))
            {
                adminsTold = true;
                PlayerSayMessage(aPlayer.Name, message, true, 1);
            }
            return adminsTold;
        }

        public void ProconChatWrite(String msg)
        {
            msg = msg.Replace(Environment.NewLine, String.Empty);
            ExecuteCommand("procon.protected.chat.write", "AdKatsLRT > " + msg);
            if (_slowmo)
            {
                _threadMasterWaitHandle.WaitOne(1000);
            }
        }

        public DateTime DateTimeFromEpochSeconds(Double epochSeconds)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(epochSeconds);
        }

        public void UpdateSettingPage()
        {
            SetExternalPluginSetting("AdKatsLRT", "UpdateSettings", "Update");
        }

        public void SetExternalPluginSetting(String pluginName, String settingName, String settingValue)
        {
            if (String.IsNullOrEmpty(pluginName) || String.IsNullOrEmpty(settingName) || settingValue == null)
            {
                Log.Error("Required inputs null or empty in setExternalPluginSetting");
                return;
            }
            ExecuteCommand("procon.protected.plugins.setVariable", pluginName, settingName, settingValue);
        }

        public string TrimStart(string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }

        private void DoBattlelogWait()
        {
            try
            {

                lock (_battlelogLocker)
                {
                    var now = DateTime.UtcNow;
                    var timeSinceLast = (now - _lastBattlelogAction);
                    var requiredWait = _battlelogWaitDuration;
                    //Reduce required wait time based on how many players are in the queue
                    if (_highRequestVolume)
                    {
                        requiredWait -= TimeSpan.FromSeconds(2);
                    }
                    //Wait between battlelog actions
                    if ((now - _lastBattlelogAction) < requiredWait)
                    {
                        var remainingWait = requiredWait - timeSinceLast;
                        Thread.Sleep(remainingWait);
                    }
                    //Log the request frequency
                    now = DateTime.UtcNow;
                    lock (_BattlelogActionTimes)
                    {
                        _BattlelogActionTimes.Enqueue(now);
                        while (NowDuration(_BattlelogActionTimes.Peek()).TotalMinutes > 4)
                        {
                            _BattlelogActionTimes.Dequeue();
                        }
                        if (_BattlelogActionTimes.Any() && NowDuration(_lastBattlelogFrequencyMessage).TotalSeconds > 30)
                        {
                            if (_isTestingAuthorized)
                            {
                                var frequency = Math.Round(_BattlelogActionTimes.Count() / 4.0, 2);
                                Log.Info("Average battlelog request frequency: " + frequency + " r/m");
                            }
                            _lastBattlelogFrequencyMessage = DateTime.UtcNow;
                        }
                    }
                    _lastBattlelogAction = now;
                }
            }
            catch (Exception e)
            {
                Log.Exception("Error while performing battlelog wait.", e);
                Thread.Sleep(_battlelogWaitDuration);
            }
        }

        public TimeSpan NowDuration(DateTime diff)
        {
            return (DateTime.UtcNow - diff).Duration();
        }

        public class AdKatsServer
        {
            public Int64 ServerID;
            public Int64 ServerGroup;
            public String ServerIP;
            public String ServerName;
            public String ServerType = "UNKNOWN";
            public Int64 GameID = -1;
            public String ConnectionState;
            public Boolean CommanderEnabled;
            public Boolean FairFightEnabled;
            public Boolean ForceReloadWholeMags;
            public Boolean HitIndicatorEnabled;
            public String GamePatchVersion = "UNKNOWN";
            public Int32 MaxSpectators = -1;
            public CServerInfo InfoObject { get; private set; }
            private DateTime _infoObjectTime = DateTime.UtcNow;

            private AdKatsLRT _plugin;

            public AdKatsServer(AdKatsLRT plugin)
            {
                _plugin = plugin;
            }

            public void SetInfoObject(CServerInfo infoObject)
            {
                InfoObject = infoObject;
                ServerName = infoObject.ServerName;
                _infoObjectTime = DateTime.UtcNow;
            }

            public TimeSpan GetRoundElapsedTime()
            {
                if (InfoObject == null)
                {
                    return TimeSpan.Zero;
                }
                return TimeSpan.FromSeconds(InfoObject.RoundTime) + (DateTime.UtcNow - _infoObjectTime);
            }
        }

        public class AdKatsLoadout
        {
            public HashSet<String> AllKitItemIDs;

            public WarsawItem KitGadget1;
            public WarsawItem KitGadget2;
            public WarsawItem KitGrenade;
            public WarsawItem KitItemPrimary;
            public WarsawItem KitItemSidearm;
            public WarsawItem KitKnife;
            public readonly Dictionary<String, WarsawItem> LoadoutItems;
            public readonly Dictionary<String, WarsawVehicle> LoadoutVehicles;
            public readonly Dictionary<String, WarsawVehicle> LoadoutRCONVehicles;
            public readonly Dictionary<String, WarsawItem> VehicleItems;
            public String Name;
            public String PersonaID;
            public WarsawKit SelectedKit;

            public AdKatsLoadout()
            {
                LoadoutItems = new Dictionary<String, WarsawItem>();
                LoadoutVehicles = new Dictionary<String, WarsawVehicle>();
                LoadoutRCONVehicles = new Dictionary<String, WarsawVehicle>();
                VehicleItems = new Dictionary<String, WarsawItem>();
                AllKitItemIDs = new HashSet<String>();
            }
        }

        public class ProcessObject
        {
            public String ProcessReason;
            public Boolean ProcessManual;
            public DateTime ProcessTime;
            public AdKatsSubscribedPlayer ProcessPlayer;
        }

        public class AdKatsSubscribedPlayer
        {
            public Boolean AA;
            public String ConversationPartner;
            public Int32 Deaths;
            public String GUID;
            public Int64 ID;
            public Int32 InfractionPoints;
            public String IP;
            public Boolean IsAdmin;
            public Double KDR;
            public Int32 Kills;
            public TimeSpan LastAction = TimeSpan.Zero;
            public TimeSpan LastForgive = TimeSpan.Zero;
            public TimeSpan LastPunishment = TimeSpan.Zero;
            public Boolean LoadoutEnforced = false;
            public Boolean LoadoutValid = true;
            public Boolean LoadoutForced;
            public Boolean LoadoutIgnored;
            public String Name;
            public Boolean Online;
            public String PBGUID;
            public String PersonaID;
            public String ClanTag;
            public Double Ping;
            public Boolean Punished;
            public Int32 Rank;
            public Boolean Reported;
            public Double Reputation;
            public String Role;
            public Int32 Score;
            public Boolean SpawnedOnce;
            public Int32 Squad;
            public Int32 Team;
            public String Type;

            public AdKatsLoadout Loadout;
            public HashSet<String> WatchedVehicles;
            public DateTime LastUsage;
            public Int32 LoadoutKills;
            public Int32 MaxDeniedItems;
            public Int32 LoadoutChecks;
            public Int32 SkippedChecks;

            public AdKatsSubscribedPlayer()
            {
                WatchedVehicles = new HashSet<String>();
                LastUsage = DateTime.UtcNow;
            }

            public String GetVerboseName()
            {
                return ((String.IsNullOrEmpty(ClanTag)) ? ("") : ("[" + ClanTag + "]")) + Name;
            }
        }

        public class MapMode
        {
            public Int32 MapModeID;
            public String ModeKey;
            public String MapKey;
            public String ModeName;
            public String MapName;

            public MapMode(Int32 mapModeID, String modeKey, String mapKey, String modeName, String mapName)
            {
                MapModeID = mapModeID;
                ModeKey = modeKey;
                MapKey = mapKey;
                ModeName = modeName;
                MapName = mapName;
            }
        }

        public void PopulateMapModes()
        {
            _availableMapModes = new List<MapMode> {
                new MapMode(1, "ConquestLarge0", "MP_Abandoned", "Conquest Large", "Zavod 311"),
                new MapMode(2, "ConquestLarge0", "MP_Damage", "Conquest Large", "Lancang Dam"),
                new MapMode(3, "ConquestLarge0", "MP_Flooded", "Conquest Large", "Flood Zone"),
                new MapMode(4, "ConquestLarge0", "MP_Journey", "Conquest Large", "Golmud Railway"),
                new MapMode(5, "ConquestLarge0", "MP_Naval", "Conquest Large", "Paracel Storm"),
                new MapMode(6, "ConquestLarge0", "MP_Prison", "Conquest Large", "Operation Locker"),
                new MapMode(7, "ConquestLarge0", "MP_Resort", "Conquest Large", "Hainan Resort"),
                new MapMode(8, "ConquestLarge0", "MP_Siege", "Conquest Large", "Siege of Shanghai"),
                new MapMode(9, "ConquestLarge0", "MP_TheDish", "Conquest Large", "Rogue Transmission"),
                new MapMode(10, "ConquestLarge0", "MP_Tremors", "Conquest Large", "Dawnbreaker"),
                new MapMode(11, "ConquestSmall0", "MP_Abandoned", "Conquest Small", "Zavod 311"),
                new MapMode(12, "ConquestSmall0", "MP_Damage", "Conquest Small", "Lancang Dam"),
                new MapMode(13, "ConquestSmall0", "MP_Flooded", "Conquest Small", "Flood Zone"),
                new MapMode(14, "ConquestSmall0", "MP_Journey", "Conquest Small", "Golmud Railway"),
                new MapMode(15, "ConquestSmall0", "MP_Naval", "Conquest Small", "Paracel Storm"),
                new MapMode(16, "ConquestSmall0", "MP_Prison", "Conquest Small", "Operation Locker"),
                new MapMode(17, "ConquestSmall0", "MP_Resort", "Conquest Small", "Hainan Resort"),
                new MapMode(18, "ConquestSmall0", "MP_Siege", "Conquest Small", "Siege of Shanghai"),
                new MapMode(19, "ConquestSmall0", "MP_TheDish", "Conquest Small", "Rogue Transmission"),
                new MapMode(20, "ConquestSmall0", "MP_Tremors", "Conquest Small", "Dawnbreaker"),
                new MapMode(21, "Domination0", "MP_Abandoned", "Domination", "Zavod 311"),
                new MapMode(22, "Domination0", "MP_Damage", "Domination", "Lancang Dam"),
                new MapMode(23, "Domination0", "MP_Flooded", "Domination", "Flood Zone"),
                new MapMode(24, "Domination0", "MP_Journey", "Domination", "Golmud Railway"),
                new MapMode(25, "Domination0", "MP_Naval", "Domination", "Paracel Storm"),
                new MapMode(26, "Domination0", "MP_Prison", "Domination", "Operation Locker"),
                new MapMode(27, "Domination0", "MP_Resort", "Domination", "Hainan Resort"),
                new MapMode(28, "Domination0", "MP_Siege", "Domination", "Siege of Shanghai"),
                new MapMode(29, "Domination0", "MP_TheDish", "Domination", "Rogue Transmission"),
                new MapMode(30, "Domination0", "MP_Tremors", "Domination", "Dawnbreaker"),
                new MapMode(31, "Elimination0", "MP_Abandoned", "Defuse", "Zavod 311"),
                new MapMode(32, "Elimination0", "MP_Damage", "Defuse", "Lancang Dam"),
                new MapMode(33, "Elimination0", "MP_Flooded", "Defuse", "Flood Zone"),
                new MapMode(34, "Elimination0", "MP_Journey", "Defuse", "Golmud Railway"),
                new MapMode(35, "Elimination0", "MP_Naval", "Defuse", "Paracel Storm"),
                new MapMode(36, "Elimination0", "MP_Prison", "Defuse", "Operation Locker"),
                new MapMode(37, "Elimination0", "MP_Resort", "Defuse", "Hainan Resort"),
                new MapMode(38, "Elimination0", "MP_Siege", "Defuse", "Siege of Shanghai"),
                new MapMode(39, "Elimination0", "MP_TheDish", "Defuse", "Rogue Transmission"),
                new MapMode(40, "Obliteration", "MP_Abandoned", "Obliteration", "Zavod 311"),
                new MapMode(41, "Obliteration", "MP_Damage", "Obliteration", "Lancang Dam"),
                new MapMode(42, "Obliteration", "MP_Flooded", "Obliteration", "Flood Zone"),
                new MapMode(43, "Obliteration", "MP_Journey", "Obliteration", "Golmud Railway"),
                new MapMode(44, "Obliteration", "MP_Naval", "Obliteration", "Paracel Storm"),
                new MapMode(45, "Obliteration", "MP_Prison", "Obliteration", "Operation Locker"),
                new MapMode(46, "Obliteration", "MP_Resort", "Obliteration", "Hainan Resort"),
                new MapMode(47, "Obliteration", "MP_Siege", "Obliteration", "Siege of Shanghai"),
                new MapMode(48, "Obliteration", "MP_TheDish", "Obliteration", "Rogue Transmission"),
                new MapMode(49, "Obliteration", "MP_Tremors", "Obliteration", "Dawnbreaker"),
                new MapMode(50, "RushLarge0", "MP_Abandoned", "Rush", "Zavod 311"),
                new MapMode(51, "RushLarge0", "MP_Damage", "Rush", "Lancang Dam"),
                new MapMode(52, "RushLarge0", "MP_Flooded", "Rush", "Flood Zone"),
                new MapMode(53, "RushLarge0", "MP_Journey", "Rush", "Golmud Railway"),
                new MapMode(54, "RushLarge0", "MP_Naval", "Rush", "Paracel Storm"),
                new MapMode(55, "RushLarge0", "MP_Prison", "Rush", "Operation Locker"),
                new MapMode(56, "RushLarge0", "MP_Resort", "Rush", "Hainan Resort"),
                new MapMode(57, "RushLarge0", "MP_Siege", "Rush", "Siege of Shanghai"),
                new MapMode(58, "RushLarge0", "MP_TheDish", "Rush", "Rogue Transmission"),
                new MapMode(59, "RushLarge0", "MP_Tremors", "Rush", "Dawnbreaker"),
                new MapMode(60, "SquadDeathMatch0", "MP_Abandoned", "Squad Deathmatch", "Zavod 311"),
                new MapMode(61, "SquadDeathMatch0", "MP_Damage", "Squad Deathmatch", "Lancang Dam"),
                new MapMode(62, "SquadDeathMatch0", "MP_Flooded", "Squad Deathmatch", "Flood Zone"),
                new MapMode(63, "SquadDeathMatch0", "MP_Journey", "Squad Deathmatch", "Golmud Railway"),
                new MapMode(64, "SquadDeathMatch0", "MP_Naval", "Squad Deathmatch", "Paracel Storm"),
                new MapMode(65, "SquadDeathMatch0", "MP_Prison", "Squad Deathmatch", "Operation Locker"),
                new MapMode(66, "SquadDeathMatch0", "MP_Resort", "Squad Deathmatch", "Hainan Resort"),
                new MapMode(67, "SquadDeathMatch0", "MP_Siege", "Squad Deathmatch", "Siege of Shanghai"),
                new MapMode(68, "SquadDeathMatch0", "MP_TheDish", "Squad Deathmatch", "Rogue Transmission"),
                new MapMode(69, "SquadDeathMatch0", "MP_Tremors", "Squad Deathmatch", "Dawnbreaker"),
                new MapMode(70, "TeamDeathMatch0", "MP_Abandoned", "Team Deathmatch", "Zavod 311"),
                new MapMode(71, "TeamDeathMatch0", "MP_Damage", "Team Deathmatch", "Lancang Dam"),
                new MapMode(72, "TeamDeathMatch0", "MP_Flooded", "Team Deathmatch", "Flood Zone"),
                new MapMode(73, "TeamDeathMatch0", "MP_Journey", "Team Deathmatch", "Golmud Railway"),
                new MapMode(74, "TeamDeathMatch0", "MP_Naval", "Team Deathmatch", "Paracel Storm"),
                new MapMode(75, "TeamDeathMatch0", "MP_Prison", "Team Deathmatch", "Operation Locker"),
                new MapMode(76, "TeamDeathMatch0", "MP_Resort", "Team Deathmatch", "Hainan Resort"),
                new MapMode(77, "TeamDeathMatch0", "MP_Siege", "Team Deathmatch", "Siege of Shanghai"),
                new MapMode(78, "TeamDeathMatch0", "MP_TheDish", "Team Deathmatch", "Rogue Transmission"),
                new MapMode(79, "TeamDeathMatch0", "MP_Tremors", "Team Deathmatch", "Dawnbreaker"),
                new MapMode(80, "ConquestLarge0", "XP1_001", "Conquest Large", "Silk Road"),
                new MapMode(81, "ConquestLarge0", "XP1_002", "Conquest Large", "Altai Range"),
                new MapMode(82, "ConquestLarge0", "XP1_003", "Conquest Large", "Guilin Peaks"),
                new MapMode(83, "ConquestLarge0", "XP1_004", "Conquest Large", "Dragon Pass"),
                new MapMode(84, "ConquestSmall0", "XP1_001", "Conquest Small", "Silk Road"),
                new MapMode(85, "ConquestSmall0", "XP1_002", "Conquest Small", "Altai Range"),
                new MapMode(86, "ConquestSmall0", "XP1_003", "Conquest Small", "Guilin Peaks"),
                new MapMode(87, "ConquestSmall0", "XP1_004", "Conquest Small", "Dragon Pass"),
                new MapMode(88, "Domination0", "XP1_001", "Domination", "Silk Road"),
                new MapMode(89, "Domination0", "XP1_002", "Domination", "Altai Range"),
                new MapMode(90, "Domination0", "XP1_003", "Domination", "Guilin Peaks"),
                new MapMode(91, "Domination0", "XP1_004", "Domination", "Dragon Pass"),
                new MapMode(92, "Elimination0", "XP1_001", "Defuse", "Silk Road"),
                new MapMode(93, "Elimination0", "XP1_002", "Defuse", "Altai Range"),
                new MapMode(94, "Elimination0", "XP1_003", "Defuse", "Guilin Peaks"),
                new MapMode(95, "Elimination0", "XP1_004", "Defuse", "Dragon Pass"),
                new MapMode(96, "Obliteration", "XP1_001", "Obliteration", "Silk Road"),
                new MapMode(97, "Obliteration", "XP1_002", "Obliteration", "Altai Range"),
                new MapMode(98, "Obliteration", "XP1_003", "Obliteration", "Guilin Peaks"),
                new MapMode(99, "Obliteration", "XP1_004", "Obliteration", "Dragon Pass"),
                new MapMode(100, "RushLarge0", "XP1_001", "Rush", "Silk Road"),
                new MapMode(101, "RushLarge0", "XP1_002", "Rush", "Altai Range"),
                new MapMode(102, "RushLarge0", "XP1_003", "Rush", "Guilin Peaks"),
                new MapMode(103, "RushLarge0", "XP1_004", "Rush", "Dragon Pass"),
                new MapMode(104, "SquadDeathMatch0", "XP1_001", "Squad Deathmatch", "Silk Road"),
                new MapMode(105, "SquadDeathMatch0", "XP1_002", "Squad Deathmatch", "Altai Range"),
                new MapMode(106, "SquadDeathMatch0", "XP1_003", "Squad Deathmatch", "Guilin Peaks"),
                new MapMode(107, "SquadDeathMatch0", "XP1_004", "Squad Deathmatch", "Dragon Pass"),
                new MapMode(108, "TeamDeathMatch0", "XP1_001", "Team Deathmatch", "Silk Road"),
                new MapMode(109, "TeamDeathMatch0", "XP1_002", "Team Deathmatch", "Altai Range"),
                new MapMode(110, "TeamDeathMatch0", "XP1_003", "Team Deathmatch", "Guilin Peaks"),
                new MapMode(111, "TeamDeathMatch0", "XP1_004", "Team Deathmatch", "Dragon Pass"),
                new MapMode(112, "AirSuperiority0", "XP1_001", "Air Superiority", "Silk Road"),
                new MapMode(113, "AirSuperiority0", "XP1_002", "Air Superiority", "Altai Range"),
                new MapMode(114, "AirSuperiority0", "XP1_003", "Air Superiority", "Guilin Peaks"),
                new MapMode(115, "AirSuperiority0", "XP1_004", "Air Superiority", "Dragon Pass"),
                new MapMode(116, "ConquestLarge0", "XP0_Caspian", "Conquest Large", "Caspian Border 2014"),
                new MapMode(117, "ConquestLarge0", "XP0_Firestorm", "Conquest Large", "Operation Firestorm 2014"),
                new MapMode(118, "ConquestLarge0", "XP0_Metro", "Conquest Large", "Operation Metro 2014"),
                new MapMode(119, "ConquestLarge0", "XP0_Oman", "Conquest Large", "Gulf of Oman 2014"),
                new MapMode(120, "ConquestSmall0", "XP0_Caspian", "Conquest Small", "Caspian Border 2014"),
                new MapMode(121, "ConquestSmall0", "XP0_Firestorm", "Conquest Small", "Operation Firestorm 2014"),
                new MapMode(122, "ConquestSmall0", "XP0_Metro", "Conquest Small", "Operation Metro 2014"),
                new MapMode(123, "ConquestSmall0", "XP0_Oman", "Conquest Small", "Gulf of Oman 2014"),
                new MapMode(124, "Domination0", "XP0_Caspian", "Domination", "Caspian Border 2014"),
                new MapMode(125, "Domination0", "XP0_Firestorm", "Domination", "Operation Firestorm 2014"),
                new MapMode(126, "Domination0", "XP0_Metro", "Domination", "Operation Metro 2014"),
                new MapMode(127, "Domination0", "XP0_Oman", "Domination", "Gulf of Oman 2014"),
                new MapMode(128, "Elimination0", "XP0_Caspian", "Defuse", "Caspian Border 2014"),
                new MapMode(129, "Elimination0", "XP0_Firestorm", "Defuse", "Operation Firestorm 2014"),
                new MapMode(130, "Elimination0", "XP0_Metro", "Defuse", "Operation Metro 2014"),
                new MapMode(131, "Elimination0", "XP0_Oman", "Defuse", "Gulf of Oman 2014"),
                new MapMode(132, "Obliteration", "XP0_Caspian", "Obliteration", "Caspian Border 2014"),
                new MapMode(133, "Obliteration", "XP0_Firestorm", "Obliteration", "Operation Firestorm 2014"),
                new MapMode(134, "Obliteration", "XP0_Metro", "Obliteration", "Operation Metro 2014"),
                new MapMode(135, "Obliteration", "XP0_Oman", "Obliteration", "Gulf of Oman 2014"),
                new MapMode(136, "RushLarge0", "XP0_Caspian", "Rush", "Caspian Border 2014"),
                new MapMode(137, "RushLarge0", "XP0_Firestorm", "Rush", "Operation Firestorm 2014"),
                new MapMode(138, "RushLarge0", "XP0_Metro", "Rush", "Operation Metro 2014"),
                new MapMode(139, "RushLarge0", "XP0_Oman", "Rush", "Gulf of Oman 2014"),
                new MapMode(140, "SquadDeathMatch0", "XP0_Caspian", "Squad Deathmatch", "Caspian Border 2014"),
                new MapMode(141, "SquadDeathMatch0", "XP0_Firestorm", "Squad Deathmatch", "Operation Firestorm 2014"),
                new MapMode(142, "SquadDeathMatch0", "XP0_Metro", "Squad Deathmatch", "Operation Metro 2014"),
                new MapMode(143, "SquadDeathMatch0", "XP0_Oman", "Squad Deathmatch", "Gulf of Oman 2014"),
                new MapMode(144, "TeamDeathMatch0", "XP0_Caspian", "Team Deathmatch", "Caspian Border 2014"),
                new MapMode(145, "TeamDeathMatch0", "XP0_Firestorm", "Team Deathmatch", "Operation Firestorm 2014"),
                new MapMode(146, "TeamDeathMatch0", "XP0_Metro", "Team Deathmatch", "Operation Metro 2014"),
                new MapMode(147, "TeamDeathMatch0", "XP0_Oman", "Team Deathmatch", "Gulf of Oman 2014"),
                new MapMode(148, "CaptureTheFlag0", "XP0_Caspian", "CTF", "Caspian Border 2014"),
                new MapMode(149, "CaptureTheFlag0", "XP0_Firestorm", "CTF", "Operation Firestorm 2014"),
                new MapMode(150, "CaptureTheFlag0", "XP0_Metro", "CTF", "Operation Metro 2014"),
                new MapMode(151, "CaptureTheFlag0", "XP0_Oman", "CTF", "Gulf of Oman 2014"),
                new MapMode(152, "ConquestLarge0", "XP2_001", "Conquest Large", "Lost Islands"),
                new MapMode(153, "ConquestLarge0", "XP2_002", "Conquest Large", "Nansha Strike"),
                new MapMode(154, "ConquestLarge0", "XP2_003", "Conquest Large", "Wavebreaker"),
                new MapMode(155, "ConquestLarge0", "XP2_004", "Conquest Large", "Operation Mortar"),
                new MapMode(156, "ConquestSmall0", "XP2_001", "Conquest Small", "Lost Islands"),
                new MapMode(157, "ConquestSmall0", "XP2_002", "Conquest Small", "Nansha Strike"),
                new MapMode(158, "ConquestSmall0", "XP2_003", "Conquest Small", "Wavebreaker"),
                new MapMode(159, "ConquestSmall0", "XP2_004", "Conquest Small", "Operation Mortar"),
                new MapMode(160, "Domination0", "XP2_001", "Domination", "Lost Islands"),
                new MapMode(161, "Domination0", "XP2_002", "Domination", "Nansha Strike"),
                new MapMode(162, "Domination0", "XP2_003", "Domination", "Wavebreaker"),
                new MapMode(163, "Domination0", "XP2_004", "Domination", "Operation Mortar"),
                new MapMode(164, "Elimination0", "XP2_001", "Defuse", "Lost Islands"),
                new MapMode(165, "Elimination0", "XP2_002", "Defuse", "Nansha Strike"),
                new MapMode(166, "Elimination0", "XP2_003", "Defuse", "Wavebreaker"),
                new MapMode(167, "Elimination0", "XP2_004", "Defuse", "Operation Mortar"),
                new MapMode(168, "Obliteration", "XP2_001", "Obliteration", "Lost Islands"),
                new MapMode(169, "Obliteration", "XP2_002", "Obliteration", "Nansha Strike"),
                new MapMode(170, "Obliteration", "XP2_003", "Obliteration", "Wavebreaker"),
                new MapMode(171, "Obliteration", "XP2_004", "Obliteration", "Operation Mortar"),
                new MapMode(172, "RushLarge0", "XP2_001", "Rush", "Lost Islands"),
                new MapMode(173, "RushLarge0", "XP2_002", "Rush", "Nansha Strike"),
                new MapMode(174, "RushLarge0", "XP2_003", "Rush", "Wavebreaker"),
                new MapMode(175, "RushLarge0", "XP2_004", "Rush", "Operation Mortar"),
                new MapMode(176, "SquadDeathMatch0", "XP2_001", "Squad Deathmatch", "Lost Islands"),
                new MapMode(177, "SquadDeathMatch0", "XP2_002", "Squad Deathmatch", "Nansha Strike"),
                new MapMode(178, "SquadDeathMatch0", "XP2_003", "Squad Deathmatch", "Wavebreaker"),
                new MapMode(179, "SquadDeathMatch0", "XP2_004", "Squad Deathmatch", "Operation Mortar"),
                new MapMode(180, "TeamDeathMatch0", "XP2_001", "Team Deathmatch", "Lost Islands"),
                new MapMode(181, "TeamDeathMatch0", "XP2_002", "Team Deathmatch", "Nansha Strike"),
                new MapMode(182, "TeamDeathMatch0", "XP2_003", "Team Deathmatch", "Wavebreaker"),
                new MapMode(183, "TeamDeathMatch0", "XP2_004", "Team Deathmatch", "Operation Mortar"),
                new MapMode(184, "CarrierAssaultLarge0", "XP2_001", "Carrier Assault Large", "Lost Islands"),
                new MapMode(185, "CarrierAssaultLarge0", "XP2_002", "Carrier Assault Large", "Nansha Strike"),
                new MapMode(186, "CarrierAssaultLarge0", "XP2_003", "Carrier Assault Large", "Wavebreaker"),
                new MapMode(187, "CarrierAssaultLarge0", "XP2_004", "Carrier Assault Large", "Operation Mortar"),
                new MapMode(188, "CarrierAssaultSmall0", "XP2_001", "Carrier Assault Small", "Lost Islands"),
                new MapMode(189, "CarrierAssaultSmall0", "XP2_002", "Carrier Assault Small", "Nansha Strike"),
                new MapMode(190, "CarrierAssaultSmall0", "XP2_003", "Carrier Assault Small", "Wavebreaker"),
                new MapMode(191, "CarrierAssaultSmall0", "XP2_004", "Carrier Assault Small", "Operation Mortar"),
                new MapMode(192, "ConquestLarge0", "XP3_MarketPl", "Conquest Large", "Pearl Market"),
                new MapMode(193, "ConquestLarge0", "XP3_Prpganda", "Conquest Large", "Propaganda"),
                new MapMode(194, "ConquestLarge0", "XP3_UrbanGdn", "Conquest Large", "Lumphini Garden"),
                new MapMode(195, "ConquestLarge0", "XP3_WtrFront", "Conquest Large", "Sunken Dragon"),
                new MapMode(196, "ConquestSmall0", "XP3_MarketPl", "Conquest Small", "Pearl Market"),
                new MapMode(197, "ConquestSmall0", "XP3_Prpganda", "Conquest Small", "Propaganda"),
                new MapMode(198, "ConquestSmall0", "XP3_UrbanGdn", "Conquest Small", "Lumphini Garden"),
                new MapMode(199, "ConquestSmall0", "XP3_WtrFront", "Conquest Small", "Sunken Dragon"),
                new MapMode(200, "Domination0", "XP3_MarketPl", "Domination", "Pearl Market"),
                new MapMode(201, "Domination0", "XP3_Prpganda", "Domination", "Propaganda"),
                new MapMode(202, "Domination0", "XP3_UrbanGdn", "Domination", "Lumphini Garden"),
                new MapMode(203, "Domination0", "XP3_WtrFront", "Domination", "Sunken Dragon"),
                new MapMode(204, "Elimination0", "XP3_MarketPl", "Defuse", "Pearl Market"),
                new MapMode(205, "Elimination0", "XP3_Prpganda", "Defuse", "Propaganda"),
                new MapMode(206, "Elimination0", "XP3_UrbanGdn", "Defuse", "Lumphini Garden"),
                new MapMode(207, "Elimination0", "XP3_WtrFront", "Defuse", "Sunken Dragon"),
                new MapMode(208, "Obliteration", "XP3_MarketPl", "Obliteration", "Pearl Market"),
                new MapMode(209, "Obliteration", "XP3_Prpganda", "Obliteration", "Propaganda"),
                new MapMode(210, "Obliteration", "XP3_UrbanGdn", "Obliteration", "Lumphini Garden"),
                new MapMode(211, "Obliteration", "XP3_WtrFront", "Obliteration", "Sunken Dragon"),
                new MapMode(212, "RushLarge0", "XP3_MarketPl", "Rush", "Pearl Market"),
                new MapMode(213, "RushLarge0", "XP3_Prpganda", "Rush", "Propaganda"),
                new MapMode(214, "RushLarge0", "XP3_UrbanGdn", "Rush", "Lumphini Garden"),
                new MapMode(215, "RushLarge0", "XP3_WtrFront", "Rush", "Sunken Dragon"),
                new MapMode(216, "SquadDeathMatch0", "XP3_MarketPl", "Squad Deathmatch", "Pearl Market"),
                new MapMode(217, "SquadDeathMatch0", "XP3_Prpganda", "Squad Deathmatch", "Propaganda"),
                new MapMode(218, "SquadDeathMatch0", "XP3_UrbanGdn", "Squad Deathmatch", "Lumphini Garden"),
                new MapMode(219, "SquadDeathMatch0", "XP3_WtrFront", "Squad Deathmatch", "Sunken Dragon"),
                new MapMode(220, "TeamDeathMatch0", "XP3_MarketPl", "Team Deathmatch", "Pearl Market"),
                new MapMode(221, "TeamDeathMatch0", "XP3_Prpganda", "Team Deathmatch", "Propaganda"),
                new MapMode(222, "TeamDeathMatch0", "XP3_UrbanGdn", "Team Deathmatch", "Lumphini Garden"),
                new MapMode(223, "TeamDeathMatch0", "XP3_WtrFront", "Team Deathmatch", "Sunken Dragon"),
                new MapMode(224, "CaptureTheFlag0", "XP3_MarketPl", "CTF", "Pearl Market"),
                new MapMode(225, "CaptureTheFlag0", "XP3_Prpganda", "CTF", "Propaganda"),
                new MapMode(226, "CaptureTheFlag0", "XP3_UrbanGdn", "CTF", "Lumphini Garden"),
                new MapMode(227, "CaptureTheFlag0", "XP3_WtrFront", "CTF", "Sunken Dragon"),
                new MapMode(228, "Chainlink0", "XP3_MarketPl", "Chain Link", "Pearl Market"),
                new MapMode(229, "Chainlink0", "XP3_Prpganda", "Chain Link", "Propaganda"),
                new MapMode(230, "Chainlink0", "XP3_UrbanGdn", "Chain Link", "Lumphini Garden"),
                new MapMode(231, "Chainlink0", "XP3_WtrFront", "Chain Link", "Sunken Dragon"),
                new MapMode(232, "ConquestLarge0", "XP4_Arctic", "Conquest Large", "Operation Whiteout"),
                new MapMode(233, "ConquestLarge0", "XP4_SubBase", "Conquest Large", "Hammerhead"),
                new MapMode(234, "ConquestLarge0", "XP4_Titan", "Conquest Large", "Hangar 21"),
                new MapMode(235, "ConquestLarge0", "XP4_WlkrFtry", "Conquest Large", "Giants Of Karelia"),
                new MapMode(236, "ConquestSmall0", "XP4_Arctic", "Conquest Small", "Operation Whiteout"),
                new MapMode(237, "ConquestSmall0", "XP4_SubBase", "Conquest Small", "Hammerhead"),
                new MapMode(238, "ConquestSmall0", "XP4_Titan", "Conquest Small", "Hangar 21"),
                new MapMode(239, "ConquestSmall0", "XP4_WlkrFtry", "Conquest Small", "Giants Of Karelia"),
                new MapMode(240, "Domination0", "XP4_Arctic", "Domination", "Operation Whiteout"),
                new MapMode(241, "Domination0", "XP4_SubBase", "Domination", "Hammerhead"),
                new MapMode(242, "Domination0", "XP4_Titan", "Domination", "Hangar 21"),
                new MapMode(243, "Domination0", "XP4_WlkrFtry", "Domination", "Giants Of Karelia"),
                new MapMode(244, "Elimination0", "XP4_Arctic", "Defuse", "Operation Whiteout"),
                new MapMode(245, "Elimination0", "XP4_SubBase", "Defuse", "Hammerhead"),
                new MapMode(246, "Elimination0", "XP4_Titan", "Defuse", "Hangar 21"),
                new MapMode(247, "Elimination0", "XP4_WlkrFtry", "Defuse", "Giants Of Karelia"),
                new MapMode(248, "Obliteration", "XP4_Arctic", "Obliteration", "Operation Whiteout"),
                new MapMode(249, "Obliteration", "XP4_SubBase", "Obliteration", "Hammerhead"),
                new MapMode(250, "Obliteration", "XP4_Titan", "Obliteration", "Hangar 21"),
                new MapMode(251, "Obliteration", "XP4_WlkrFtry", "Obliteration", "Giants Of Karelia"),
                new MapMode(252, "RushLarge0", "XP4_Arctic", "Rush", "Operation Whiteout"),
                new MapMode(253, "RushLarge0", "XP4_SubBase", "Rush", "Hammerhead"),
                new MapMode(254, "RushLarge0", "XP4_Titan", "Rush", "Hangar 21"),
                new MapMode(255, "RushLarge0", "XP4_WlkrFtry", "Rush", "Giants Of Karelia"),
                new MapMode(256, "SquadDeathMatch0", "XP4_Arctic", "Squad Deathmatch", "Operation Whiteout"),
                new MapMode(257, "SquadDeathMatch0", "XP4_SubBase", "Squad Deathmatch", "Hammerhead"),
                new MapMode(258, "SquadDeathMatch0", "XP4_Titan", "Squad Deathmatch", "Hangar 21"),
                new MapMode(259, "SquadDeathMatch0", "XP4_WlkrFtry", "Squad Deathmatch", "Giants Of Karelia"),
                new MapMode(260, "TeamDeathMatch0", "XP4_Arctic", "Team Deathmatch", "Operation Whiteout"),
                new MapMode(261, "TeamDeathMatch0", "XP4_SubBase", "Team Deathmatch", "Hammerhead"),
                new MapMode(262, "TeamDeathMatch0", "XP4_Titan", "Team Deathmatch", "Hangar 21"),
                new MapMode(263, "TeamDeathMatch0", "XP4_WlkrFtry", "Team Deathmatch", "Giants Of Karelia"),
                new MapMode(264, "CaptureTheFlag0", "XP4_Arctic", "CTF", "Operation Whiteout"),
                new MapMode(265, "CaptureTheFlag0", "XP4_SubBase", "CTF", "Hammerhead"),
                new MapMode(266, "CaptureTheFlag0", "XP4_Titan", "CTF", "Hangar 21"),
                new MapMode(267, "CaptureTheFlag0", "XP4_WlkrFtry", "CTF", "Giants Of Karelia"),
                new MapMode(268, "SquadObliteration0", "MP_Abandoned", "Squad Obliteration", "Zavod 311"),
                new MapMode(269, "SquadObliteration0", "MP_Journey", "Squad Obliteration", "Golmud Railway"),
                new MapMode(270, "SquadObliteration0", "MP_Naval", "Squad Obliteration", "Paracel Storm"),
                new MapMode(271, "SquadObliteration0", "MP_Prison", "Squad Obliteration", "Operation Locker"),
                new MapMode(272, "SquadObliteration0", "MP_Resort", "Squad Obliteration", "Hainan Resort"),
                new MapMode(273, "SquadObliteration0", "MP_Siege", "Squad Obliteration", "Siege of Shanghai"),
                new MapMode(274, "SquadObliteration0", "MP_Tremors", "Squad Obliteration", "Dawnbreaker"),
                new MapMode(275, "ConquestLarge0", "XP5_Night_01", "Conquest Large", "Zavod:Graveyard Shift"),
                new MapMode(276, "ConquestSmall0", "XP5_Night_01", "Conquest Small", "Zavod:Graveyard Shift"),
                new MapMode(277, "Domination0", "XP5_Night_01", "Domination", "Zavod:Graveyard Shift"),
                new MapMode(278, "Obliteration", "XP5_Night_01", "Obliteration", "Zavod:Graveyard Shift"),
                new MapMode(279, "RushLarge0", "XP5_Night_01", "Rush", "Zavod:Graveyard Shift"),
                new MapMode(280, "TeamDeathMatch0", "XP5_Night_01", "Team Deathmatch", "Zavod:Graveyard Shift"),
                new MapMode(281, "ConquestLarge0", "XP6_CMP", "Conquest Large", "Operation Outbreak"),
                new MapMode(282, "ConquestSmall0", "XP6_CMP", "Conquest Small", "Operation Outbreak"),
                new MapMode(283, "Domination0", "XP6_CMP", "Domination", "Operation Outbreak"),
                new MapMode(284, "Obliteration", "XP6_CMP", "Obliteration", "Operation Outbreak"),
                new MapMode(285, "RushLarge0", "XP6_CMP", "Rush", "Operation Outbreak"),
                new MapMode(286, "SquadDeathMatch0", "XP6_CMP", "Squad Deathmatch", "Operation Outbreak"),
                new MapMode(287, "SquadDeathMatch1", "XP6_CMP", "Squad Deathmatch", "Operation Outbreak v2"),
                new MapMode(288, "TeamDeathMatch0", "XP6_CMP", "Team Deathmatch", "Operation Outbreak"),
                new MapMode(289, "TeamDeathMatch1", "XP6_CMP", "Team Deathmatch", "Operation Outbreak v2"),
                new MapMode(290, "CaptureTheFlag0", "XP6_CMP", "CTF", "Operation Outbreak"),
                new MapMode(291, "Chainlink0", "XP6_CMP", "Chain Link", "Operation Outbreak"),
                new MapMode(292, "ConquestLarge0", "XP7_Valley", "Conquest Large", "Dragon Valley 2015"),
                new MapMode(293, "ConquestSmall0", "XP7_Valley", "Conquest Small", "Dragon Valley 2015"),
                new MapMode(294, "Domination0", "XP7_Valley", "Domination", "Dragon Valley 2015"),
                new MapMode(295, "Obliteration", "XP7_Valley", "Obliteration", "Dragon Valley 2015"),
                new MapMode(296, "RushLarge0", "XP7_Valley", "Rush", "Dragon Valley 2015"),
                new MapMode(297, "SquadDeathMatch0", "XP7_Valley", "Squad Deathmatch", "Dragon Valley 2015"),
                new MapMode(298, "TeamDeathMatch0", "XP7_Valley", "Team Deathmatch", "Dragon Valley 2015"),
                new MapMode(299, "GunMaster0", "XP7_Valley", "Gun Master", "Dragon Valley 2015"),
                new MapMode(300, "AirSuperiority0", "XP7_Valley", "Air Superiority", "Dragon Valley 2015")
            };
        }

        private void PopulateWarsawRCONCodes()
        {
            //Load in all knowns WARSAW to RCON mappings for use with the autoadmin
            _WarsawRCONMappings.Clear();
            _RCONWarsawMappings.Clear();

            //DMRs
            _WarsawRCONMappings["1915356177"] = (new string[] { "U_M39EBR" }).ToList();
            _WarsawRCONMappings["4092888892"] = (new string[] { "U_SVD12" }).ToList();
            _WarsawRCONMappings["3860123089"] = (new string[] { "U_GalilACE53" }).ToList();
            _WarsawRCONMappings["1906761969"] = (new string[] { "U_QBU88" }).ToList();
            _WarsawRCONMappings["3072292273"] = (new string[] { "U_SCAR-HSV" }).ToList();
            _WarsawRCONMappings["2144050545"] = (new string[] { "U_SKS" }).ToList();
            _WarsawRCONMappings["1894217457"] = (new string[] { "U_RFB" }).ToList();
            _WarsawRCONMappings["408290737"] = (new string[] { "U_MK11" }).ToList();
            _WarsawRCONMappings["2759849572"] = (new string[] { "U_SR338" }).ToList();

            //Snipers
            _WarsawRCONMappings["2853300518"] = (new string[] { "U_GOL" }).ToList();
            _WarsawRCONMappings["2897869395"] = (new string[] { "U_M98B" }).ToList();
            _WarsawRCONMappings["2967613745"] = (new string[] { "U_Scout" }).ToList();
            _WarsawRCONMappings["1079830129"] = (new string[] { "U_FY-JS" }).ToList();
            _WarsawRCONMappings["1596514833"] = (new string[] { "U_SV98" }).ToList();
            _WarsawRCONMappings["3458855537"] = (new string[] { "U_CS-LR4" }).ToList();
            _WarsawRCONMappings["4135125553"] = (new string[] { "U_JNG90" }).ToList();
            _WarsawRCONMappings["1834910833"] = (new string[] { "U_M40A5" }).ToList();
            _WarsawRCONMappings["388555399"] = (new string[] { "U_L96A1" }).ToList();
            _WarsawRCONMappings["3555293285"] = (new string[] { "U_CS5" }).ToList();
            _WarsawRCONMappings["3081643377"] = (new string[] { "U_SRS" }).ToList();
            _WarsawRCONMappings["1710440049"] = (new string[] { "U_M200" }).ToList();

            //PDWs
            _WarsawRCONMappings["1020126577"] = (new string[] { "U_P90" }).ToList();
            _WarsawRCONMappings["2021343793"] = (new string[] { "U_MX4" }).ToList();
            _WarsawRCONMappings["763058951"] = (new string[] { "U_MP7" }).ToList();
            _WarsawRCONMappings["3382662737"] = (new string[] { "U_PP2000" }).ToList();
            _WarsawRCONMappings["1030797713"] = (new string[] { "U_CBJ-MS" }).ToList();
            _WarsawRCONMappings["2128008177"] = (new string[] { "U_UMP45" }).ToList();
            _WarsawRCONMappings["2665548081"] = (new string[] { "U_Scorpion" }).ToList();
            _WarsawRCONMappings["4227814065"] = (new string[] { "U_UMP9" }).ToList();
            _WarsawRCONMappings["4208515505"] = (new string[] { "U_MagpulPDR" }).ToList();
            _WarsawRCONMappings["3204230182"] = (new string[] { "U_ASVal" }).ToList();
            _WarsawRCONMappings["3188912241"] = (new string[] { "U_JS2" }).ToList();
            _WarsawRCONMappings["1689098981"] = (new string[] { "U_MPX" }).ToList();
            _WarsawRCONMappings["821324708"] = (new string[] { "U_SR2" }).ToList();
            _WarsawRCONMappings["2203062595"] = (new string[] { "U_Groza-4" }).ToList();

            //Assault Rifles
            _WarsawRCONMappings["3059253169"] = (new string[] { "U_GalilACE23" }).ToList();
            _WarsawRCONMappings["2643258020"] = (new string[] { "U_AR160" }).ToList();
            _WarsawRCONMappings["4279753681"] = (new string[] { "U_M416" }).ToList();
            _WarsawRCONMappings["2829366246"] = (new string[] { "U_F2000" }).ToList();
            _WarsawRCONMappings["319908497"] = (new string[] { "U_SteyrAug" }).ToList();
            _WarsawRCONMappings["2815752497"] = (new string[] { "U_FAMAS" }).ToList();
            _WarsawRCONMappings["2826786481"] = (new string[] { "U_CZ805" }).ToList();
            _WarsawRCONMappings["819903973"] = (new string[] { "U_Bulldog" }).ToList();
            _WarsawRCONMappings["1687010979"] = (new string[] { "U_AN94" }).ToList();
            _WarsawRCONMappings["234564305"] = (new string[] { "U_QBZ951" }).ToList();
            _WarsawRCONMappings["4242111601"] = (new string[] { "U_SAR21" }).ToList();
            _WarsawRCONMappings["669091281"] = (new string[] { "U_AEK971" }).ToList();
            _WarsawRCONMappings["174491409"] = (new string[] { "U_SCAR-H" }).ToList();
            _WarsawRCONMappings["821324709"] = (new string[] { "U_L85A2" }).ToList();
            _WarsawRCONMappings["3590299697"] = (new string[] { "U_AK12" }).ToList();
            _WarsawRCONMappings["3119417649"] = (new string[] { "U_M16A4" }).ToList();

            //Carbines
            _WarsawRCONMappings["1896957361"] = (new string[] { "U_SG553LB" }).ToList();
            _WarsawRCONMappings["2978429873"] = (new string[] { "U_G36C" }).ToList();
            _WarsawRCONMappings["3313614225"] = (new string[] { "U_AK5C" }).ToList();
            _WarsawRCONMappings["2864846705"] = (new string[] { "U_AKU12" }).ToList();
            _WarsawRCONMappings["2830105186"] = (new string[] { "dlSHTR" }).ToList();
            _WarsawRCONMappings["1987438087"] = (new string[] { "U_MTAR21" }).ToList();
            _WarsawRCONMappings["3192695217"] = (new string[] { "U_MTAR21" }).ToList();
            _WarsawRCONMappings["2152664305"] = (new string[] { "U_Type95B" }).ToList();
            _WarsawRCONMappings["326957379"] = (new string[] { "U_Groza-1" }).ToList();
            _WarsawRCONMappings["3448559030"] = (new string[] { "U_GalilACE" }).ToList();
            _WarsawRCONMappings["2082703729"] = (new string[] { "U_GalilACE52" }).ToList();
            _WarsawRCONMappings["458988977"] = (new string[] { "U_ACR" }).ToList();
            _WarsawRCONMappings["2713563633"] = (new string[] { "U_A91" }).ToList();
            _WarsawRCONMappings["3192695217"] = (new string[] { "U_M4A1" }).ToList();

            //LMGs
            _WarsawRCONMappings["3852069478"] = (new string[] { "U_M60E4" }).ToList();
            _WarsawRCONMappings["1321048617"] = (new string[] { "U_RPK-74" }).ToList();
            _WarsawRCONMappings["2572144625"] = (new string[] { "U_M240" }).ToList();
            _WarsawRCONMappings["2749423953"] = (new string[] { "U_M249" }).ToList();
            _WarsawRCONMappings["1810379907"] = (new string[] { "U_L86A1" }).ToList();
            _WarsawRCONMappings["3900816465"] = (new string[] { "U_LSAT" }).ToList();
            _WarsawRCONMappings["3000062065"] = (new string[] { "U_Pecheneg" }).ToList();
            _WarsawRCONMappings["2005518564"] = (new string[] { "U_AWS" }).ToList();
            _WarsawRCONMappings["2048507580"] = (new string[] { "U_RPK12" }).ToList();
            _WarsawRCONMappings["302761745"] = (new string[] { "U_Type88" }).ToList();
            _WarsawRCONMappings["2403214513"] = (new string[] { "U_MG4" }).ToList();
            _WarsawRCONMappings["4226187761"] = (new string[] { "U_QBB95" }).ToList();
            _WarsawRCONMappings["3179658801"] = (new string[] { "U_Ultimax" }).ToList();

            //Handguns
            _WarsawRCONMappings["3942150929"] = (new string[] { "U_FN57" }).ToList();
            _WarsawRCONMappings["335786382"] = (new string[] { "U_SaddlegunSnp" }).ToList();
            _WarsawRCONMappings["3730491953"] = (new string[] { "U_MP443" }).ToList();
            _WarsawRCONMappings["3300350865"] = (new string[] { "U_Taurus44" }).ToList();
            _WarsawRCONMappings["1276385329"] = (new string[] { "U_M93R" }).ToList();
            _WarsawRCONMappings["1518880753"] = (new string[] { "U_CZ75" }).ToList();
            _WarsawRCONMappings["264887569"] = (new string[] { "U_M9" }).ToList();
            _WarsawRCONMappings["944904529"] = (new string[] { "U_P226" }).ToList();
            _WarsawRCONMappings["3537147505"] = (new string[] { "U_QSZ92" }).ToList();
            _WarsawRCONMappings["1322096241"] = (new string[] { "U_HK45C" }).ToList();
            _WarsawRCONMappings["1715838468"] = (new string[] { "U_SW40" }).ToList();
            _WarsawRCONMappings["3430469957"] = (new string[] { "U_Unica6" }).ToList();
            _WarsawRCONMappings["908783077"] = (new string[] { "U_DesertEagle" }).ToList();
            _WarsawRCONMappings["2363034673"] = (new string[] { "U_MP412Rex" }).ToList();
            _WarsawRCONMappings["37082993"] = (new string[] { "U_Glock18" }).ToList();
            _WarsawRCONMappings["2608762737"] = (new string[] { "U_M1911" }).ToList();

            //Shotguns
            _WarsawRCONMappings["1589481582"] = (new string[] { "U_SAIGA_20K" }).ToList();
            _WarsawRCONMappings["2942558833"] = (new string[] { "U_QBS09" }).ToList();
            _WarsawRCONMappings["3528666216"] = (new string[] { "U_SteyrAug_M26_Slug", "U_SCAR-H_M26_Slug", "U_SAR21_M26_Slug", "U_QBZ951_M26_Slug", "U_M416_M26_Slug", "U_M26Mass_Slug", "U_M16A4_M26_Slug", "U_CZ805_M26_Slug", "U_AR160_M26_Slug", "U_AK12_M26_Slug", "U_AEK971_M26_Slug" }).ToList();
            _WarsawRCONMappings["1848317553"] = (new string[] { "U_M1014" }).ToList();
            _WarsawRCONMappings["2930960995"] = (new string[] { "U_870" }).ToList();
            _WarsawRCONMappings["4054082865"] = (new string[] { "U_HAWK" }).ToList();
            _WarsawRCONMappings["4292296724"] = (new string[] { "U_M26Mass_Frag" }).ToList();
            _WarsawRCONMappings["4174194330"] = (new string[] { "U_M26Mass_Flechette" }).ToList();
            _WarsawRCONMappings["94493788"] = (new string[] { "U_DBV12" }).ToList();
            _WarsawRCONMappings["3044954406"] = (new string[] { "U_DAO12" }).ToList();
            _WarsawRCONMappings["3221408826"] = (new string[] { "U_M26Mass", "U_SteyrAug_M26_Buck", "U_SCAR-H_M26_Buck", "U_SAR21_M26_Buck", "U_QBZ951_M26_Buck", "U_M416_M26_Buck", "U_M16A4_M26_Buck", "U_CZ805_M26_Buck", "U_AR160_M26_Buck", "U_AK12_M26_Buck", "U_AEK971_M26_Buck" }).ToList();
            _WarsawRCONMappings["3044954406"] = (new string[] { "U_DAO12" }).ToList();
            _WarsawRCONMappings["4204280241"] = (new string[] { "U_SPAS12" }).ToList();
            _WarsawRCONMappings["3661909297"] = (new string[] { "U_SerbuShorty" }).ToList();
            _WarsawRCONMappings["623014897"] = (new string[] { "U_UTAS" }).ToList();

            //Gadgets
            _WarsawRCONMappings["1364316986"] = (new string[] { "U_M224", "M224" }).ToList();
            _WarsawRCONMappings["4169380388"] = (new string[] { "U_XM25_Smoke" }).ToList();
            _WarsawRCONMappings["3042980396"] = (new string[] { "UCAV" }).ToList();
            _WarsawRCONMappings["2698261753"] = (new string[] { "U_SLAM" }).ToList();
            _WarsawRCONMappings["3645048844"] = (new string[] { "AA Mine" }).ToList();
            _WarsawRCONMappings["4077480573"] = (new string[] { "U_Claymore" }).ToList();
            _WarsawRCONMappings["3398724484"] = (new string[] { "U_Claymore_Recon" }).ToList();
            _WarsawRCONMappings["3076304839"] = (new string[] { "U_C4" }).ToList();
            _WarsawRCONMappings["2375254013"] = (new string[] { "U_C4_Support" }).ToList();
            _WarsawRCONMappings["3054368924"] = (new string[] { "U_XM25_Flechette" }).ToList();
            _WarsawRCONMappings["1005841160"] = (new string[] { "U_XM25" }).ToList();
            _WarsawRCONMappings["704874518"] = (new string[] { "U_M15" }).ToList();

            //Launchers
            _WarsawRCONMappings["2880824228"] = (new string[] { "U_AN94_M320_FLASH_v1", "U_SteyrAug_M320_FLASH", "U_SCAR-H_M320_FLASH", "U_SAR21_M320_FLASH", "U_QBZ951_M320_FLASH", "U_M416_M320_FLASH", "U_M320_FLASH", "U_M16A4_M320_FLASH", "U_L85A2_M320_FLASH_V2", "U_CZ805_M320_FLASH", "U_AR160_M320_FLASH", "U_AK12_M320_FLASH", "U_AEK971_M320_FLASH" }).ToList();
            _WarsawRCONMappings["4084720679"] = (new string[] { "U_SP_M320_HE", "U_AN94_M320_HE_v1", "U_SteyrAug_M320_HE", "U_SCAR-H_M320_HE", "U_SAR21_M320_HE", "U_QBZ951_M320_HE", "U_M416_M320_HE", "U_M320_HE", "U_M16A4_M320_HE", "U_L85A2_M320_HE_V2", "U_CZ805_M320_HE", "U_AR160_M320_HE", "U_AK12_M320_HE", "U_AEK971_M320_HE" }).ToList();
            _WarsawRCONMappings["1723239682"] = (new string[] { "U_AN94_M320_SHG_v1", "U_SteyrAug_M320_SHG", "U_SCAR-H_M320_SHG", "U_SAR21_M320_SHG", "U_QBZ951_M320_SHG", "U_M416_M320_SHG", "U_M320_SHG", "U_M16A4_M320_SHG", "U_L85A2_M320_SHG_V2", "U_CZ805_M320_SHG", "U_AR160_M320_SHG", "U_AK12_M320_SHG", "U_AEK971_M320_SHG" }).ToList();
            _WarsawRCONMappings["434964836"] = (new string[] { "U_AN94_M320_3GL_v1", "U_CZ805_M320_3GL", "U_SteyrAug_M320_3GL", "U_SCAR-H_M320_3GL", "U_SAR21_M320_3GL", "U_QBZ951_M320_3GL", "U_M416_M320_3GL", "U_M320_3GL", "U_M16A4_M320_3GL", "U_L85A2_M320_3GL_V2", "U_CZ605_M320_3GL", "U_AR160_M320_3GL", "U_AK12_M320_3GL", "U_AEK971_M320_3GL" }).ToList();
            _WarsawRCONMappings["863588126"] = (new string[] { "U_AN94_M320_SMK_v1", "U_SteyrAug_M320_SMK", "U_SCAR-H_M320_SMK", "U_SAR21_M320_SMK", "U_QBZ951_M320_SMK", "U_M416_M320_SMK", "U_M320_SMK", "U_M16A4_M320_SMK", "U_L85A2_M320_SMK_V2", "U_CZ805_M320_SMK", "U_AR160_M320_SMK", "U_AK12_M320_SMK", "U_AEK971_M320_SMK" }).ToList();
            _WarsawRCONMappings["335737287"] = (new string[] { "U_AN94_M320_LVG_v1", "U_SteyrAug_M320_LVG", "U_SCAR-H_M320_LVG", "U_SAR21_M320_LVG", "U_QBZ951_M320_LVG", "U_M416_M320_LVG", "U_M320_LVG", "U_M16A4_M320_LVG", "U_L85A2_M320_LVG_V2", "U_CZ805_M320_LVG", "U_AR160_M320_LVG", "U_AK12_M320_LVG", "U_AEK971_M320_LVG" }).ToList();

            //Special
            _WarsawRCONMappings["2887915611"] = (new string[] { "U_Defib" }).ToList();
            _WarsawRCONMappings["2324320899"] = (new string[] { "U_Repairtool" }).ToList();
            _WarsawRCONMappings["312950893"] = (new string[] { "U_BallisticShield" }).ToList();
            _WarsawRCONMappings["3416970831"] = (new string[] { "Death", "EODBot" }).ToList();
            //            _WarsawRCONMappings["3881213532"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3913003056"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["1278769027"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3214146841"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2930902275"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3981629339"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2833476239"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2765835967"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2358565358"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2130832595"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2065907307"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["4098378714"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["714992459"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3154558973"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3194673210"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["3332841661"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["27972285"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["2709653572"] = (new string[] { "Melee" }).ToList();
            //            _WarsawRCONMappings["6240291"] = (new string[] { "Melee" }).ToList();

            //Grenades
            _WarsawRCONMappings["3767777089"] = (new string[] { "U_Grenade_RGO" }).ToList();
            _WarsawRCONMappings["3133964300"] = (new string[] { "U_M18" }).ToList();
            _WarsawRCONMappings["2842275721"] = (new string[] { "U_M34" }).ToList();
            _WarsawRCONMappings["2916285594"] = (new string[] { "U_Handflare" }).ToList();
            _WarsawRCONMappings["69312926"] = (new string[] { "U_V40" }).ToList();
            _WarsawRCONMappings["2670747868"] = (new string[] { "U_M67" }).ToList();
            _WarsawRCONMappings["1779756455"] = (new string[] { "U_Flashbang" }).ToList();

            //Rocket
            _WarsawRCONMappings["3194075724"] = (new string[] { "U_FIM92" }).ToList();
            _WarsawRCONMappings["3713498991"] = (new string[] { "U_Sa18IGLA" }).ToList();
            _WarsawRCONMappings["20932301"] = (new string[] { "U_RPG7" }).ToList();
            _WarsawRCONMappings["601919388"] = (new string[] { "U_NLAW" }).ToList();
            _WarsawRCONMappings["1359435055"] = (new string[] { "U_FGM148" }).ToList();
            _WarsawRCONMappings["3177196226"] = (new string[] { "U_SRAW" }).ToList();
            _WarsawRCONMappings["1782193877"] = (new string[] { "U_SMAW" }).ToList();

            //Populate the reverse mapping dictionary
            foreach (KeyValuePair<String, List<String>> warsawRCON in _WarsawRCONMappings)
            {
                String warsawID = warsawRCON.Key;
                List<String> matchingRCONCodes = warsawRCON.Value;

                foreach (String RCONCode in matchingRCONCodes)
                {
                    List<String> warsawIDs;
                    if (!_RCONWarsawMappings.TryGetValue(RCONCode, out warsawIDs))
                    {
                        warsawIDs = new List<String>();
                        _RCONWarsawMappings[RCONCode] = warsawIDs;
                    }
                    if (!warsawIDs.Contains(warsawID))
                    {
                        warsawIDs.Add(warsawID);
                    }
                }
            }
        }

        public class WarsawItem
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed categoryType does not make any modifications
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public Dictionary<String, Dictionary<String, WarsawItemAccessory>> AccessoriesAllowed;
            public Dictionary<String, WarsawItemAccessory> AccessoriesAssigned;
            public String CategoryReadable;
            public String Category;
            public String CategoryTypeReadable;
            public String CategoryType;
            public String Desc;
            public String Name;
            public String Slug;
            public String WarsawID;
            public WarsawVehicle AssignedVehicle;

            public WarsawItem()
            {
                AccessoriesAssigned = new Dictionary<string, WarsawItemAccessory>();
                AccessoriesAllowed = new Dictionary<String, Dictionary<String, WarsawItemAccessory>>();
            }
        }

        public class WarsawVehicle
        {
            public String CategoryReadable;
            public String Category;
            public String CategoryTypeReadable;
            public String CategoryType;
            public Int32 SlotIndexPrimary = -1;
            public readonly Dictionary<String, WarsawItem> AllowedPrimaries;
            public WarsawItem AssignedPrimary;
            public Int32 SlotIndexSecondary = -1;
            public readonly Dictionary<String, WarsawItem> AllowedSecondaries;
            public WarsawItem AssignedSecondary;
            public Int32 SlotIndexSecondaryGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedSecondariesGunner;
            public WarsawItem AssignedSecondaryGunner;
            public Int32 SlotIndexCountermeasure = -1;
            public readonly Dictionary<String, WarsawItem> AllowedCountermeasures;
            public WarsawItem AssignedCountermeasure;
            public Int32 SlotIndexOptic = -1;
            public readonly Dictionary<String, WarsawItem> AllowedOptics;
            public WarsawItem AssignedOptic;
            public Int32 SlotIndexOpticGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedOpticsGunner;
            public WarsawItem AssignedOpticGunner;
            public Int32 SlotIndexUpgrade = -1;
            public readonly Dictionary<String, WarsawItem> AllowedUpgrades;
            public WarsawItem AssignedUpgrade;
            public Int32 SlotIndexUpgradeGunner = -1;
            public readonly Dictionary<String, WarsawItem> AllowedUpgradesGunner;
            public WarsawItem AssignedUpgradeGunner;
            public HashSet<String> LinkedRCONCodes;

            public WarsawVehicle()
            {
                AllowedPrimaries = new Dictionary<String, WarsawItem>();
                AllowedSecondaries = new Dictionary<String, WarsawItem>();
                AllowedSecondariesGunner = new Dictionary<String, WarsawItem>();
                AllowedCountermeasures = new Dictionary<String, WarsawItem>();
                AllowedOptics = new Dictionary<String, WarsawItem>();
                AllowedOpticsGunner = new Dictionary<String, WarsawItem>();
                AllowedUpgrades = new Dictionary<String, WarsawItem>();
                AllowedUpgradesGunner = new Dictionary<String, WarsawItem>();
                LinkedRCONCodes = new HashSet<String>();
            }
        }

        public class WarsawKit
        {
            public enum Type
            {
                Assault,
                Engineer,
                Support,
                Recon
            }

            public Type KitType;
            public readonly Dictionary<String, WarsawItem> KitAllowedPrimary;
            public readonly Dictionary<String, WarsawItem> KitAllowedSecondary;
            public readonly Dictionary<String, WarsawItem> KitAllowedGadget1;
            public readonly Dictionary<String, WarsawItem> KitAllowedGadget2;
            public readonly Dictionary<String, WarsawItem> KitAllowedGrenades;
            public readonly Dictionary<String, WarsawItem> KitAllowedKnife;

            public WarsawKit()
            {
                KitAllowedPrimary = new Dictionary<String, WarsawItem>();
                KitAllowedSecondary = new Dictionary<String, WarsawItem>();
                KitAllowedGadget1 = new Dictionary<String, WarsawItem>();
                KitAllowedGadget2 = new Dictionary<String, WarsawItem>();
                KitAllowedGrenades = new Dictionary<String, WarsawItem>();
                KitAllowedKnife = new Dictionary<String, WarsawItem>();
            }
        }

        public class WarsawItemAccessory
        {
            //only take entries with numeric IDs
            //Parsed category removes leading "WARSAW_ID_P_CAT_", replaces "_" with " ", and lower cases the rest
            //Parsed name removes leading "WARSAW_ID_P_INAME_", "WARSAW_ID_P_WNAME_", or "WARSAW_ID_P_ANAME_", replaces "_" with " ", and lower cases the rest
            //Parsed slug removes ending digit if one exists, replaces "_" with " ", replaces "-" with " ", and upper cases the rest
            //If expansion exists assign it, if not, ignore
            public String Category;
            public String CategoryReadable;
            public String Name;
            public String Slug;
            public String WarsawID;
        }

        public class WarsawLibrary
        {
            public readonly Dictionary<String, WarsawItem> Items;
            public readonly Dictionary<String, WarsawItemAccessory> ItemAccessories;
            public readonly Dictionary<String, WarsawVehicle> Vehicles;
            public readonly Dictionary<String, WarsawItem> VehicleUnlocks;
            public readonly WarsawKit KitAssault;
            public readonly WarsawKit KitEngineer;
            public readonly WarsawKit KitSupport;
            public readonly WarsawKit KitRecon;

            public WarsawLibrary()
            {
                Items = new Dictionary<String, WarsawItem>();
                ItemAccessories = new Dictionary<String, WarsawItemAccessory>();
                Vehicles = new Dictionary<String, WarsawVehicle>();
                VehicleUnlocks = new Dictionary<String, WarsawItem>();
                KitAssault = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Assault,
                };
                KitEngineer = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Engineer,
                };
                KitSupport = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Support,
                };
                KitRecon = new WarsawKit()
                {
                    KitType = WarsawKit.Type.Recon,
                };
            }
        }

        public class Logger
        {
            private readonly AdKatsLRT _plugin;
            public Int32 DebugLevel { get; set; }
            public Boolean VerboseErrors { get; set; }

            public Logger(AdKatsLRT plugin)
            {
                _plugin = plugin;
            }

            private void WriteConsole(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.pluginconsole.write", "[^b" + _plugin.GetType().Name + "^n] " + msg);
            }

            private void WriteChat(String msg)
            {
                _plugin.ExecuteCommand("procon.protected.chat.write", _plugin.GetType().Name + " > " + msg);
            }

            public void Debug(String msg, Int32 level)
            {
                if (DebugLevel >= level)
                {
                    if (DebugLevel >= 8)
                    {
                        WriteConsole("[" + level + "-" + new StackFrame(1).GetMethod().Name + "-" + ((String.IsNullOrEmpty(Thread.CurrentThread.Name)) ? ("Main") : (Thread.CurrentThread.Name)) + Thread.CurrentThread.ManagedThreadId + "] " + msg);
                    }
                    else
                    {
                        WriteConsole(msg);
                    }
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

            public void Exception(String msg, Exception e)
            {
                this.Exception(msg, e, 1);
            }

            public void Exception(String msg, Exception e, Int32 level)
            {
                //Opening
                string exceptionMessage = "^b^8EXCEPTION-" +//Plugin version
                                          Int32.Parse(_plugin.GetPluginVersion().Replace(".", ""));
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
            }

            public void Chat(String msg)
            {
                msg = msg.Replace(Environment.NewLine, String.Empty);
                WriteChat(msg);
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
    }

    // GZipWebClient removed — replaced by Flurl HTTP client (Procon v2)
}
