using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Flurl;
using Flurl.Http;

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
        // PushBullet, Weapon Dictionary, Email Handler (lines 63631-64518 from original AdKats.cs)
        // ===========================================================================================

        public class PushBulletHandler
        {
            public enum Target
            {
                Private,
                Channel
            }

            public AdKats Plugin;
            public String AccessToken;
            public Target DefaultTarget = Target.Private;
            public String DefaultChannelTag;

            public PushBulletHandler(AdKats plugin)
            {
                Plugin = plugin;
            }

            public void PushReport(ARecord record)
            {
                if (record.target_player == null)
                {
                    Plugin.SendMessageToSource(record, "Unable to send report email. No target player found.");
                    return;
                }
                Plugin.Log.Debug(() => "Sending PushBullet report [" + record.command_numeric + "] on " + record.GetTargetNames(), 3);
                String title = record.GetTargetNames() + " reported in [" + Plugin.GameVersion + "] " + Plugin._serverInfo.ServerName.Substring(0, Math.Min(15, Plugin._serverInfo.ServerName.Length - 1));
                StringBuilder bb = new StringBuilder();
                bb.Append("AdKats player report [" + record.command_numeric + "]");
                bb.AppendLine();
                bb.AppendLine();
                bb.Append(record.GetSourceName() + " reported " + record.GetTargetNames() + " for " + record.record_message);
                bb.AppendLine();
                bb.AppendLine();
                bb.Append(Plugin._serverInfo.ServerName);
                PushDefault(title, bb.ToString());
            }

            public void PushDefault(String title, String body)
            {
                switch (DefaultTarget)
                {
                    case Target.Private:
                        PushPrivate(title, body);
                        break;
                    case Target.Channel:
                        PushChannel(title, body, DefaultChannelTag);
                        break;
                    default:
                        Plugin.Log.Error("Pushbullet configured with invalid target.");
                        break;
                }
            }

            public void PushPrivate(String title, String body)
            {
                WebResponse response = null;
                try
                {
                    if (String.IsNullOrEmpty(AccessToken))
                    {
                        Plugin.Log.Error("PushBullet token empty! Unable to push private note.");
                        return;
                    }
                    if (String.IsNullOrEmpty(title))
                    {
                        Plugin.Log.Error("PushBullet note title empty! Unable to push private note.");
                        return;
                    }
                    if (String.IsNullOrEmpty(body))
                    {
                        Plugin.Log.Error("PushBullet note body empty! Unable to push private note.");
                        return;
                    }
                    WebRequest request = WebRequest.Create("https://api.pushbullet.com/v2/pushes");
                    request.Method = "POST";
                    request.Headers.Add("Access-Token", AccessToken);
                    request.ContentType = "application/json";
                    String jsonBody = JSON.JsonEncode(new Hashtable {
                        {"active", true},
                        {"type", "note"},
                        {"sender_name", "AdKats-" + Plugin._serverInfo.ServerID},
                        {"title", title},
                        {"body", body}
                    });
                    byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                    request.ContentLength = byteArray.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(byteArray, 0, byteArray.Length);
                    requestStream.Close();
                }
                catch (WebException e)
                {
                    using (response = e.Response)
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        Plugin.Log.Info("RESPONSE: " + reader.ReadToEnd());
                    }
                    Plugin.Log.HandleException(new AException("Error sending private PushBullet note.", e));
                }
            }

            public void PushChannel(String title, String body, String channelTag)
            {
                WebResponse response = null;
                try
                {
                    if (String.IsNullOrEmpty(AccessToken))
                    {
                        Plugin.Log.Error("PushBullet token empty! Unable to push channel note.");
                        return;
                    }
                    if (String.IsNullOrEmpty(channelTag))
                    {
                        Plugin.Log.Error("PushBullet channel tag empty! Unable to push channel note.");
                        return;
                    }
                    if (String.IsNullOrEmpty(title))
                    {
                        Plugin.Log.Error("PushBullet note title empty! Unable to push channel note.");
                        return;
                    }
                    if (String.IsNullOrEmpty(body))
                    {
                        Plugin.Log.Error("PushBullet note body empty! Unable to push channel note.");
                        return;
                    }
                    WebRequest request = WebRequest.Create("https://api.pushbullet.com/v2/pushes");
                    request.Method = "POST";
                    request.Headers.Add("Access-Token", AccessToken);
                    request.ContentType = "application/json";
                    String jsonBody = JSON.JsonEncode(new Hashtable {
                        {"active", true},
                        {"type", "note"},
                        {"sender_name", "AdKats-" + Plugin._serverInfo.ServerID},
                        {"channel_tag", channelTag},
                        {"title", title},
                        {"body", body}
                    });
                    byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                    request.ContentLength = byteArray.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(byteArray, 0, byteArray.Length);
                    requestStream.Close();
                }
                catch (WebException e)
                {
                    using (response = e.Response)
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        Plugin.Log.Info("RESPONSE: " + reader.ReadToEnd());
                    }
                    Plugin.Log.HandleException(new AException("Error sending private PushBullet note.", e));
                }
            }
        }

        public class AWeaponDictionary
        {
            public AdKats _plugin;

            public readonly Dictionary<String, AWeapon> Weapons = new Dictionary<String, AWeapon>();
            public String AllDamageTypeEnumString = "";
            public String InfantryDamageTypeEnumString = "";
            public String AllWeaponNameEnumString = "";
            public String InfantryWeaponNameEnumString = "";

            public AWeaponDictionary(AdKats plugin)
            {
                _plugin = plugin;

                try
                {
                    //Fill the damage type setting enum string
                    Random random = new Random(Environment.TickCount);
                    InfantryDamageTypeEnumString = String.Empty;
                    foreach (DamageTypes damageType in Enum.GetValues(typeof(DamageTypes))
                                                           .Cast<DamageTypes>()
                                                           .Where(type => type != DamageTypes.Nonlethal &&
                                                                          type != DamageTypes.Suicide &&
                                                                          type != DamageTypes.VehicleAir &&
                                                                          type != DamageTypes.VehicleHeavy &&
                                                                          type != DamageTypes.VehicleLight &&
                                                                          type != DamageTypes.VehiclePersonal &&
                                                                          type != DamageTypes.VehicleStationary &&
                                                                          type != DamageTypes.VehicleTransport &&
                                                                          type != DamageTypes.VehicleWater)
                                                           .OrderBy(type => type.ToString()))
                    {
                        if (String.IsNullOrEmpty(InfantryDamageTypeEnumString))
                        {
                            InfantryDamageTypeEnumString += "enum.InfantryDamageTypeEnum_" + random.Next(100000, 999999) + "(";
                        }
                        else
                        {
                            InfantryDamageTypeEnumString += "|";
                        }
                        InfantryDamageTypeEnumString += damageType;
                    }
                    InfantryDamageTypeEnumString += ")";

                    AllDamageTypeEnumString = String.Empty;
                    foreach (DamageTypes damageType in Enum.GetValues(typeof(DamageTypes))
                                                           .Cast<DamageTypes>()
                                                           .Where(type => type != DamageTypes.Nonlethal &&
                                                                          type != DamageTypes.Suicide)
                                                           .OrderBy(type => type.ToString()))
                    {
                        if (String.IsNullOrEmpty(AllDamageTypeEnumString))
                        {
                            AllDamageTypeEnumString += "enum.AllDamageTypeEnum_" + random.Next(100000, 999999) + "(";
                        }
                        else
                        {
                            AllDamageTypeEnumString += "|";
                        }
                        AllDamageTypeEnumString += damageType;
                    }
                    AllDamageTypeEnumString += ")";
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while creating weapon dictionary.", e));
                }
            }

            public Boolean PopulateDictionaries()
            {
                try
                {
                    // Populate the weapon types
                    foreach (Weapon weapon in _plugin.GetWeaponDefines())
                    {
                        if (weapon != null)
                        {
                            // Valid weapon, add damage type to the dictionary
                            AWeapon dWeapon = null;
                            if (!Weapons.TryGetValue(weapon.Name, out dWeapon))
                            {
                                dWeapon = new AWeapon()
                                {
                                    Game = _plugin.GameVersion,
                                    Code = weapon.Name
                                };
                                Weapons[dWeapon.Code] = dWeapon;
                            }
                            dWeapon.Damage = weapon.Damage;
                            // Fixing invalid weapon damages
                            if (dWeapon.Code == "dlSHTR")
                            {
                                // This is the phantom bow. Change it to sniper rifle damage type.
                                dWeapon.Damage = DamageTypes.SniperRifle;
                            }
                            if (dWeapon.Code == "U_SR338")
                            {
                                // This is a DMR incorrectly categorized as sniper damage
                                dWeapon.Damage = DamageTypes.DMR;
                            }
                            if (dWeapon.Code == "U_BallisticShield")
                            {
                                // This is the shield incorrectly categorized as impact damage.
                                dWeapon.Damage = DamageTypes.Melee;
                            }
                            if (dWeapon.Code.ToLower() == "roadkill")
                            {
                                // ToLower needed because of different values between bf3/bf4
                                dWeapon.Damage = DamageTypes.Impact;
                            }
                        }
                    }
                    Hashtable weaponNames = FetchAWeaponNames();
                    if (weaponNames == null)
                    {
                        return false;
                    }
                    Hashtable gameWeaponNames = (Hashtable)weaponNames[_plugin.GameVersion.ToString()];
                    if (gameWeaponNames == null)
                    {
                        _plugin.Log.Error("Weapons for " + _plugin.GameVersion + " not found in weapon name library.");
                        return false;
                    }
                    foreach (DictionaryEntry currentWeapon in gameWeaponNames)
                    {
                        //Create new construct
                        String weaponCode = (String)currentWeapon.Key;
                        String shortName = (String)((Hashtable)currentWeapon.Value)["readable_short"];
                        String longName = (String)((Hashtable)currentWeapon.Value)["readable_long"];
                        AWeapon dWeapon = null;
                        if (!Weapons.TryGetValue(weaponCode, out dWeapon))
                        {
                            dWeapon = new AWeapon()
                            {
                                Game = _plugin.GameVersion,
                                Code = weaponCode
                            };
                            Weapons[dWeapon.Code] = dWeapon;
                        }
                        //Add the weapon names
                        dWeapon.Code = weaponCode;
                        dWeapon.ShortName = shortName;
                        dWeapon.LongName = longName;
                    }

                    //Fill the weapon name enum string
                    Random random = new Random(Environment.TickCount);
                    InfantryWeaponNameEnumString = String.Empty;
                    foreach (var weaponName in Weapons.Values.Where(weapon => !String.IsNullOrEmpty(weapon.ShortName) &&
                                                                              weapon.Damage != DamageTypes.None &&
                                                                              weapon.Damage != DamageTypes.Nonlethal &&
                                                                              weapon.Damage != DamageTypes.Suicide &&
                                                                              weapon.Damage != DamageTypes.VehicleAir &&
                                                                              weapon.Damage != DamageTypes.VehicleHeavy &&
                                                                              weapon.Damage != DamageTypes.VehicleLight &&
                                                                              weapon.Damage != DamageTypes.VehiclePersonal &&
                                                                              weapon.Damage != DamageTypes.VehicleStationary &&
                                                                              weapon.Damage != DamageTypes.VehicleTransport &&
                                                                              weapon.Damage != DamageTypes.VehicleWater)
                                                             .OrderBy(weapon => weapon.Damage)
                                                             .ThenBy(weapon => weapon.ShortName)
                                                             .Select(weapon => weapon.Damage.ToString() + "\\" + weapon.ShortName)
                                                             .Distinct())
                    {
                        if (String.IsNullOrEmpty(InfantryWeaponNameEnumString))
                        {
                            InfantryWeaponNameEnumString += "enum.InfantryWeaponNameEnum_" + random.Next(100000, 999999) + "(None|";
                        }
                        else
                        {
                            InfantryWeaponNameEnumString += "|";
                        }
                        InfantryWeaponNameEnumString += weaponName;
                    }
                    InfantryWeaponNameEnumString += ")";

                    AllWeaponNameEnumString = String.Empty;
                    foreach (var weaponName in Weapons.Values.Where(weapon => !String.IsNullOrEmpty(weapon.ShortName) &&
                                                                              weapon.Damage != DamageTypes.None &&
                                                                              weapon.Damage != DamageTypes.Nonlethal &&
                                                                              weapon.Damage != DamageTypes.Suicide)
                                                             .OrderBy(weapon => weapon.Damage)
                                                             .ThenBy(weapon => weapon.ShortName)
                                                             .Select(weapon => weapon.Damage.ToString() + "\\" + weapon.ShortName)
                                                             .Distinct())
                    {
                        if (String.IsNullOrEmpty(AllWeaponNameEnumString))
                        {
                            AllWeaponNameEnumString += "enum.AllWeaponNameEnum_" + random.Next(100000, 999999) + "(None|";
                        }
                        else
                        {
                            AllWeaponNameEnumString += "|";
                        }
                        AllWeaponNameEnumString += weaponName;
                    }
                    AllWeaponNameEnumString += ")";
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while populating weapon name cache", e));
                }
                return true;
            }

            private Hashtable FetchAWeaponNames()
            {
                _plugin.Log.Debug(() => "Entering FetchAWeaponNames", 7);
                Hashtable weaponNames = null;
                String downloadString;
                _plugin.Log.Debug(() => "Fetching weapon names...", 2);
                try
                {
                    downloadString = _plugin.Util.HttpDownload("https://raw.githubusercontent.com/Hedius/E4GLAdKats/main/adkatsweaponnames.json" + "?cacherand=" + Environment.TickCount);
                    _plugin.Log.Debug(() => "Weapon names fetched.", 1);
                }
                catch (Exception)
                {
                    try
                    {
                        downloadString = _plugin.Util.HttpDownload("https://adkats.e4gl.com/adkatsweaponnames.json" + "?cacherand=" + Environment.TickCount);
                        _plugin.Log.Debug(() => "Weapon names fetched from backup location.", 1);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
                try
                {
                    weaponNames = (Hashtable)JSON.JsonDecode(downloadString);
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while parsing reputation definitions.", e));
                }
                _plugin.Log.Debug(() => "Exiting FetchAWeaponNames", 7);
                return weaponNames;
            }

            public List<String> GetWeaponCodesOfDamageType(DamageTypes damage)
            {
                try
                {
                    if (damage == DamageTypes.None)
                    {
                        _plugin.Log.HandleException(new AException("Damage type was None when fetching weapons of damage type."));
                        return new List<string>();
                    }
                    return Weapons.Values.Where(weapon => weapon.Damage == damage).Select(weapon => weapon.Code).ToList();
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting damage type.", e));
                }
                return new List<string>();
            }

            public DamageTypes GetDamageTypeByWeaponCode(String weaponCode)
            {
                try
                {
                    if (String.IsNullOrEmpty(weaponCode))
                    {
                        _plugin.Log.HandleException(new AException("weaponCode was empty/null when fetching weapon damage type."));
                        return DamageTypes.None;
                    }
                    var weapon = Weapons.Values.FirstOrDefault(dWeapon => dWeapon.Code == weaponCode);
                    if (weapon == null)
                    {
                        _plugin.Log.HandleException(new AException("No weapon defined for code " + weaponCode + " when fetching damage type. Is your DEF file updated?"));
                        return DamageTypes.None;
                    }
                    return weapon.Damage;
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting damage type for weapon code.", e));
                }
                return DamageTypes.None;
            }

            public String GetWeaponCodeByShortName(String weaponShortName)
            {
                try
                {
                    if (!String.IsNullOrEmpty(weaponShortName))
                    {
                        foreach (var weapon in Weapons.Values)
                        {
                            if (weapon != null &&
                                weapon.ShortName == weaponShortName)
                            {
                                return weapon.Code;
                            }
                        }
                    }
                    _plugin.Log.HandleException(new AException("Unable to get weapon CODE for short NAME '" + weaponShortName + "', in " + Weapons.Count() + " weapons."));
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting weapon code for short name.", e));
                }
                return null;
            }

            public String GetShortWeaponNameByCode(String weaponCode)
            {
                try
                {
                    AWeapon weaponName = null;
                    if (String.IsNullOrEmpty(weaponCode))
                    {
                        _plugin.Log.HandleException(new AException("weaponCode was null when fetching weapon name"));
                        return null;
                    }
                    Weapons.TryGetValue(weaponCode, out weaponName);
                    if (weaponName == null)
                    {
                        _plugin.Log.HandleException(new AException("Unable to get weapon short NAME for CODE '" + weaponCode + "', in " + Weapons.Count() + " weapons."));
                        return weaponCode;
                    }
                    return weaponName.ShortName;
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting short weapon name for code.", e));
                }
                return null;
            }

            public String GetLongWeaponNameByCode(String weaponCode)
            {
                try
                {
                    AWeapon weaponName = null;
                    if (String.IsNullOrEmpty(weaponCode))
                    {
                        _plugin.Log.HandleException(new AException("weaponCode was null when fetching weapon name"));
                        return null;
                    }
                    Weapons.TryGetValue(weaponCode, out weaponName);
                    if (weaponName == null)
                    {
                        _plugin.Log.HandleException(new AException("Unable to get weapon long NAME for CODE '" + weaponCode + "', in " + Weapons.Count() + " weapons."));
                        return weaponCode;
                    }
                    return weaponName.LongName;
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while getting long weapon name for code.", e));
                }
                return null;
            }

            public class AWeapon
            {
                public GameVersionEnum Game;
                public DamageTypes Damage = DamageTypes.None;
                public String Code;
                public String ShortName;
                public String LongName;
            }
        }

        public class EmailHandler
        {
            private readonly Queue<MailMessage> _EmailProcessingQueue = new Queue<MailMessage>();
            public readonly EventWaitHandle _EmailProcessingWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
            public String CustomHTMLAddition;
            public AdKats Plugin;
            public List<String> RecipientEmails = new List<string>();
            public String SMTPPassword = "";
            public Int32 SMTPPort = 587;
            public String SMTPServer = "";
            public String SMTPUser = "";
            public String SenderEmail = "";
            public Boolean UseSSL = true;
            private Thread _EmailProcessingThread;

            public EmailHandler(AdKats plugin)
            {
                Plugin = plugin;
                switch (Plugin.GameVersion)
                {
                    case GameVersionEnum.BF3:
                        CustomHTMLAddition = @"<br><a href='http://battlelog.battlefield.com/bf3/user/%player_name%/'>BF3 Battlelog Profile</a><br>
<br><a href='http://bf3stats.com/stats_pc/%player_name%'>BF3Stats Profile</a><br>
<br><a href='http://history.anticheatinc.com/bf3/?searchvalue=%player_name%'>AntiCheat, INC. Search</a><br>
<br><a href='http://i-stats.net/index.php?action=pcheck&game=BF3&player=%player_name%'>I-Stats Search</a><br>
<br><a href='http://www.team-des-fra.fr/CoM/bf3.php?p=%player_name%'>TeamDes Search</a><br>
<br><a href='http://cheatometer.hedix.de/?p=%player_name%'>Hedix Search</a><br>";
                        break;
                    case GameVersionEnum.BF4:
                        CustomHTMLAddition = @"<br><a href='http://battlelog.battlefield.com/bf4/de/user/%player_name%/'>BF4 Battlelog Profile</a><br>
<br><a href='http://bf4stats.com/pc/%player_name%'>BF4Stats Profile</a><br>
<br><a href='http://history.anticheatinc.com/bf4/?searchvalue=%player_name%'>AntiCheat, INC. Search</a><br>";
                        break;
                    default:
                        CustomHTMLAddition = "";
                        break;
                }
            }

            public void SendReport(ARecord record)
            {
                try
                {
                    if (Plugin.FetchOnlineAdminSoldiers().Any() && false)
                    {
                        Plugin.Log.Warn("Online admins detected, report email aborted.");
                        return;
                    }
                    if (record.target_player == null)
                    {
                        Plugin.SendMessageToSource(record, "Unable to send report email. No target player found.");
                        return;
                    }
                    //Create a new thread to handle keep-alive
                    //This thread will remain running for the duration the layer is online
                    Thread emailSendingThread = new Thread(new ThreadStart(delegate
                    {
                        try
                        {
                            Thread.CurrentThread.Name = "EmailSending";
                            String subject = String.Empty;
                            String body = String.Empty;

                            StringBuilder sb = new StringBuilder();
                            if (String.IsNullOrEmpty(Plugin._serverInfo.ServerName))
                            {
                                //Unable to send report email, server id unknown
                                return;
                            }
                            subject = record.GetTargetNames() + " reported in [" + Plugin.GameVersion + "] " + Plugin._serverInfo.ServerName;
                            sb.Append("<h1>AdKats " + Plugin.GameVersion + " Player Report [" + record.command_numeric + "]</h1>");
                            sb.Append("<h2>" + Plugin._serverInfo.ServerName + "</h2>");
                            sb.Append("<h3>" + DateTime.Now + " ProCon Time</h3>");
                            sb.Append("<h3>" + record.GetSourceName() + " has reported " + record.GetTargetNames() + " for " + record.record_message + "</h3>");
                            sb.Append("<p>");
                            CPlayerInfo playerInfo = record.target_player.fbpInfo;
                            int numReports = Plugin._PlayerReports.Count(aRecord => aRecord.target_player.player_id == record.target_player.player_id &&
                                                                                    aRecord.TargetSession == aRecord.target_player.ActiveSession);
                            sb.Append("Reported " + numReports + " times during their current session.<br/>");
                            sb.Append("Has " + Plugin.FetchPoints(record.target_player, false, true) + " infraction points.<br/>");
                            sb.Append("Score: " + playerInfo.Score + "<br/>");
                            sb.Append("Kills: " + playerInfo.Kills + "<br/>");
                            sb.Append("Deaths: " + playerInfo.Deaths + "<br/>");
                            sb.Append("Kdr: " + playerInfo.Kdr + "<br/>");
                            sb.Append("Ping: " + playerInfo.Ping + "<br/>");
                            sb.Append("</p>");
                            sb.Append("<p>");
                            sb.Append("SoldierName: " + playerInfo.SoldierName + "<br/>");
                            sb.Append("EA GUID: " + playerInfo.GUID + "<br/>");
                            if (record.target_player.PBPlayerInfo != null)
                            {
                                sb.Append("PB GUID: " + record.target_player.PBPlayerInfo.GUID + "<br/>");
                                // sb.Append("IP: " + record.target_player.PBPlayerInfo.Ip.Split(':')[0] + "<br/>");
                                sb.Append("Country: " + record.target_player.PBPlayerInfo.PlayerCountry + "<br/>");
                            }
                            String processedCustomHTML = Plugin.ReplacePlayerInformation(CustomHTMLAddition, record.target_player);
                            processedCustomHTML = processedCustomHTML.Replace("%map_name%", Plugin.GetCurrentReadableMap());
                            processedCustomHTML = processedCustomHTML.Replace("%mode_name%", Plugin.GetCurrentReadableMode());
                            sb.Append(processedCustomHTML);
                            sb.Append("</p>");
                            if (record.target_player != null)
                            {
                                sb.Append("<table>");
                                sb.Append(@"<thead><td>Time</td><td>Player</td><td>Message</td></thead>");
                                sb.Append("<tbody>");
                                if (record.source_player != null)
                                {
                                    foreach (KeyValuePair<DateTime, KeyValuePair<string, string>> chatLine in Plugin.FetchConversation(record.source_player.player_id, record.target_player.player_id, 30, 7))
                                    {
                                        sb.Append("<tr>");
                                        sb.Append("<td>" + chatLine.Key.ToShortDateString() + " " + chatLine.Key.ToShortTimeString() + "</td>");
                                        sb.Append("<td>" + chatLine.Value.Key + "</td>");
                                        sb.Append("<td>" + chatLine.Value.Value + "</td>");
                                        sb.Append("</tr>");
                                    }
                                }
                                else
                                {
                                    foreach (KeyValuePair<DateTime, string> chatLine in Plugin.FetchChat(record.target_player.player_id, 30, 7))
                                    {
                                        sb.Append("<tr>");
                                        sb.Append("<td>" + chatLine.Key.ToShortDateString() + " " + chatLine.Key.ToShortTimeString() + "</td>");
                                        sb.Append("<td>" + record.GetTargetNames() + "</td>");
                                        sb.Append("<td>" + chatLine.Value + "</td>");
                                        sb.Append("</tr>");
                                    }
                                }
                                sb.Append("</tbody>");
                                sb.Append("</table>");
                            }

                            body = sb.ToString();


                            EmailWrite(subject, body);
                        }
                        catch (Exception e)
                        {
                            Plugin.Log.HandleException(new AException("Error in email sending thread.", e));
                        }
                        Plugin.Threading.StopWatchdog();
                    }));
                    //Start the thread
                    Plugin.Threading.StartWatchdog(emailSendingThread);
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error when sending email.", e));
                }
            }

            private void EmailWrite(String subject, String body)
            {
                try
                {
                    MailMessage email = new MailMessage();

                    email.From = new MailAddress(SenderEmail, "AdKats Report System");

                    Boolean someAdded = false;
                    lock (Plugin._userCache)
                    {
                        foreach (AUser aUser in Plugin._userCache.Values)
                        {
                            //Check for not null and default values
                            if (Plugin.UserIsAdmin(aUser) && !String.IsNullOrEmpty(aUser.user_email))
                            {
                                if (Regex.IsMatch(aUser.user_email, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                                {
                                    email.Bcc.Add(new MailAddress(aUser.user_email));
                                    someAdded = true;
                                }
                                else
                                {
                                    Plugin.Log.Error("Error in user email address: " + aUser.user_email);
                                }
                            }
                        }
                        foreach (String extraEmail in RecipientEmails)
                        {
                            if (String.IsNullOrEmpty(extraEmail.Trim()))
                            {
                                continue;
                            }

                            if (Regex.IsMatch(extraEmail, @"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"))
                            {
                                email.Bcc.Add(new MailAddress(extraEmail));
                                someAdded = true;
                            }
                            else
                            {
                                Plugin.Log.Error("Error in extra email address: " + extraEmail);
                            }
                        }
                    }
                    if (!someAdded)
                    {
                        Plugin.Log.Error("Unable to send email. No users with emails have access to player interaction commands.");
                        return;
                    }

                    email.Subject = subject;
                    email.Body = body;
                    email.IsBodyHtml = true;
                    email.BodyEncoding = Encoding.UTF8;

                    QueueEmailForSending(email);
                }
                catch (Exception e)
                {
                    Plugin.Log.Error("Error while sending email: " + e);
                }
            }

            private void QueueEmailForSending(MailMessage email)
            {
                Plugin.Log.Debug(() => "Entering QueueEmailForSending", 7);
                try
                {
                    if (Plugin._pluginEnabled)
                    {
                        Plugin.Log.Debug(() => "Preparing to queue email for processing", 6);
                        lock (_EmailProcessingQueue)
                        {
                            _EmailProcessingQueue.Enqueue(email);
                            Plugin.Log.Debug(() => "Email queued for processing", 6);
                            //Start the processing thread if not already running
                            if (_EmailProcessingThread == null || !_EmailProcessingThread.IsAlive)
                            {
                                _EmailProcessingThread = new Thread(EmailProcessingThreadLoop)
                                {
                                    IsBackground = true
                                };
                                Plugin.Threading.StartWatchdog(_EmailProcessingThread);
                            }
                            _EmailProcessingWaitHandle.Set();
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error while queueing email for processing.", e));
                }
                Plugin.Log.Debug(() => "Exiting QueueEmailForSending", 7);
            }

            public void EmailProcessingThreadLoop()
            {
                try
                {
                    Plugin.Log.Debug(() => "Starting Email Handling Thread", 1);
                    Thread.CurrentThread.Name = "EmailProcessing";
                    DateTime loopStart = Plugin.UtcNow();
                    while (true)
                    {
                        try
                        {
                            Plugin.Log.Debug(() => "Entering Email Handling Thread Loop", 7);
                            if (!Plugin._pluginEnabled)
                            {
                                Plugin.Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                                break;
                            }

                            //Get all unprocessed inbound emails
                            Queue<MailMessage> inboundEmailMessages = new Queue<MailMessage>();
                            if (_EmailProcessingQueue.Any())
                            {
                                Plugin.Log.Debug(() => "Preparing to lock inbound mail queue to retrive new mail", 7);
                                lock (_EmailProcessingQueue)
                                {
                                    Plugin.Log.Debug(() => "Inbound mail found. Grabbing.", 6);
                                    //Grab all mail in the queue
                                    inboundEmailMessages = new Queue<MailMessage>(_EmailProcessingQueue.ToArray());
                                    //Clear the queue for next run
                                    _EmailProcessingQueue.Clear();
                                }
                            }
                            else
                            {
                                Plugin.Log.Debug(() => "No inbound mail. Waiting for Input.", 6);
                                //Wait for input
                                if ((Plugin.UtcNow() - loopStart).TotalMilliseconds > 1000)
                                {
                                    Plugin.Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((Plugin.UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                                }
                                _EmailProcessingWaitHandle.Reset();
                                _EmailProcessingWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                                loopStart = Plugin.UtcNow();
                                continue;
                            }

                            //Loop through all mails in order that they came in
                            while (inboundEmailMessages.Any())
                            {
                                if (!Plugin._pluginEnabled)
                                {
                                    break;
                                }
                                Plugin.Log.Debug(() => "begin reading mail", 6);
                                MailMessage message = inboundEmailMessages.Dequeue();
                                if (Plugin.Log.DebugLevel >= 5)
                                {
                                    Plugin.Log.Write("server: " + SMTPServer);
                                    Plugin.Log.Write("port: " + SMTPPort);
                                    Plugin.Log.Write("user/pass: " + ((!String.IsNullOrEmpty(SMTPUser) && !String.IsNullOrEmpty(SMTPPassword)) ? "OK" : "INVALID"));
                                    Plugin.Log.Write("details sender: " + message.Sender);
                                    Plugin.Log.Write("details from: " + message.From);
                                    Plugin.Log.Write("details to: " + message.To);
                                    Plugin.Log.Write("details cc: " + message.CC);
                                    Plugin.Log.Write("details bcc: " + message.Bcc);
                                    Plugin.Log.Write("details subject: " + message.Subject);
                                    Plugin.Log.Write("details body: " + message.Body);
                                }
                                //Dequeue the first/next mail
                                SmtpClient smtp = new SmtpClient(SMTPServer, SMTPPort)
                                {
                                    EnableSsl = UseSSL,
                                    Timeout = 10000,
                                    DeliveryMethod = SmtpDeliveryMethod.Network,
                                    UseDefaultCredentials = false,
                                    Credentials = new NetworkCredential(SMTPUser, SMTPPassword)
                                };
                                smtp.SendCompleted += new SendCompletedEventHandler(smtp_SendCompleted);

                                Plugin.Log.Debug(() => "Sending notification email. Please wait.", 1);

                                smtp.Send(message);

                                Plugin.Log.Debug(() => "A notification email has been sent.", 1);

                                if (inboundEmailMessages.Any())
                                {
                                    //Wait 5 seconds between loops
                                    Plugin.Threading.Wait(5000);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (e is ThreadAbortException)
                            {
                                Plugin.Log.HandleException(new AException("mail processing thread aborted. Exiting."));
                                break;
                            }
                            Plugin.Log.HandleException(new AException("Error occured in mail processing thread. skipping loop.", e));
                        }
                    }
                    Plugin.Log.Debug(() => "Ending mail Processing Thread", 1);
                    Plugin.Threading.StopWatchdog();
                }
                catch (Exception e)
                {
                    Plugin.Log.HandleException(new AException("Error occured in mail processing thread.", e));
                }
            }

            private void smtp_SendCompleted(object sender, AsyncCompletedEventArgs e)
            {
                if (e.Cancelled == true || e.Error != null)
                {
                    Plugin.Log.HandleException(new AException("Error occured in mail processing. Sending Canceled.", e.Error));
                }
            }
        }


        // ===========================================================================================
        // Discord Manager, TeamSpeak Client Viewer (lines 64689-67684 from original AdKats.cs)
        // ===========================================================================================

        public class DiscordManager
        {

            //Plugin
            public AdKats _plugin;
            //Settings
            public Boolean Enabled;
            public String APIUrl = "https://discord.com/";
            public String ServerID;
            public String[] ChannelNames = { };
            public Boolean DebugMembers;
            public Boolean DebugService;
            public VoipJoinDisplayType JoinDisplay = VoipJoinDisplayType.Disabled;
            public String JoinMessage = "%playerusername% joined discord! Welcome!";
            private TimeSpan _UpdateDuration = TimeSpan.FromSeconds(29);
            public String ReportWebhookUrl;
            public String WatchlistWebhookUrl;
            public List<String> RoleIDsToMentionReport = new List<String>();
            public List<String> RoleIDsToMentionWatchlist = new List<String>();
            //Vars
            public String ServerName;
            public String InstantInvite;
            private readonly Dictionary<String, DiscordChannel> Channels = new Dictionary<String, DiscordChannel>();
            private readonly Dictionary<String, DiscordMember> Members = new Dictionary<String, DiscordMember>();
            public DateTime LastUpdate = DateTime.UtcNow - TimeSpan.FromSeconds(30);
            public Int32 ConnectionIssueCount = 0;

            public DiscordManager(AdKats plugin)
            {
                _plugin = plugin;
                RunDiscordManagerMainThread();
            }

            public void Enable()
            {
                Enabled = true;
                UpdateDiscordServerInfo();
            }

            public void Disable()
            {
                Enabled = false;
                Channels.Clear();
                Members.Clear();
            }

            public List<DiscordChannel> GetChannels()
            {
                return Channels.Values.ToList();
            }

            public List<DiscordMember> GetMembers(Boolean onlyActive, Boolean onlyVoice, Boolean onlyChannels)
            {
                try
                {
                    if (!Members.Any())
                    {
                        return new List<DiscordMember>();
                    }
                    var resultMembers = Members.Values.Where(aMember => !aMember.Bot);
                    if (onlyActive)
                    {
                        resultMembers = resultMembers.Where(aMember => aMember.Status == "online");
                    }
                    if (onlyVoice)
                    {
                        resultMembers = resultMembers.Where(aMember => aMember.Channel != null);
                    }
                    if (onlyChannels)
                    {
                        resultMembers = resultMembers.Where(aMember => aMember.Channel != null && (!ChannelNames.Any() || String.IsNullOrEmpty(ChannelNames.FirstOrDefault()) || ChannelNames.Contains(aMember.Channel.Name)));
                    }
                    return resultMembers.ToList();
                }
                catch (Exception)
                {
                    return new List<DiscordMember>();
                }
            }

            private void RunDiscordManagerMainThread()
            {
                var discordMainThread = new Thread(new ThreadStart(delegate
                {
                    Thread.CurrentThread.Name = "DiscordManagerMainThread";
                    Thread.Sleep(TimeSpan.FromMilliseconds(250));
                    // Main Thread Loop
                    while (true)
                    {
                        try
                        {
                            if (Enabled)
                            {
                                // Update the server information at interval
                                if (_plugin.NowDuration(LastUpdate) > _UpdateDuration)
                                {
                                    if (DebugService)
                                    {
                                        _plugin.Log.Info("Ready to update discord server info.");
                                    }
                                    var results = UpdateDiscordServerInfo();
                                    if (DebugService)
                                    {
                                        _plugin.Log.Info("Discord server info updated. Success: " + results);
                                    }
                                }
                            }
                            // Sleep until the next execution is needed
                            Thread.Sleep(TimeSpan.FromSeconds(30));
                        }
                        catch (Exception e)
                        {
                            _plugin.Log.HandleException(new AException("Error in discord manager main thread loop. Skipping current loop.", e));
                            Thread.Sleep(TimeSpan.FromSeconds(30));
                        }
                    }
                }));
                discordMainThread.Start();
            }

            private Boolean UpdateDiscordServerInfo()
            {
                _plugin.Log.Debug(() => "Entering UpdateDiscordServerInfo", 7);
                var success = false;
                try
                {
                    _plugin.Log.Debug(() => "Preparing to fetch discord server information.", 7);

                    //Get the widget URL
                    var widgetURL = GetWidgetURL();
                    //Attempt to fetch and parse
                    if (!String.IsNullOrEmpty(widgetURL))
                    {
                        try
                        {
                            List<DiscordMember> clientInfo = new List<DiscordMember>();
                            String clientResponse = _plugin.Util.HttpDownload(widgetURL);
                            Hashtable responseJSON = null;
                            try
                            {
                                // Remove surrogate codepoint values as raw text
                                var tester = new Regex(@"[\\][u][d][8-9a-f][0-9a-f][0-9a-f]", RegexOptions.IgnoreCase);
                                clientResponse = tester.Replace(clientResponse, String.Empty);
                                responseJSON = (Hashtable)JSON.JsonDecode(clientResponse);
                            }
                            catch (Exception e)
                            {
                                _plugin.Log.HandleException(new AException("Error processing JSON", e));
                                return false;
                            }
                            if (!responseJSON.ContainsKey("id") ||
                                !responseJSON.ContainsKey("name") ||
                                !responseJSON.ContainsKey("instant_invite") ||
                                !responseJSON.ContainsKey("channels") ||
                                !responseJSON.ContainsKey("members"))
                            {
                                _plugin.Log.Warn("Discord JSON did not contain required elements.");
                                clientResponse = clientResponse.Length <= 500 ? clientResponse : clientResponse.Substring(0, 500);
                                _plugin.Log.Warn(clientResponse);
                                return false;
                            }
                            if (DebugService)
                            {
                                _plugin.Log.Warn("Debug printing the discord client response.");
                                clientResponse = clientResponse.Length <= 500 ? clientResponse : clientResponse.Substring(0, 500);
                                _plugin.Log.Warn(clientResponse);
                            }
                            // Globals
                            ServerID = (String)responseJSON["id"];
                            ServerName = (String)responseJSON["name"];
                            InstantInvite = (String)responseJSON["instant_invite"];
                            // Channels
                            ArrayList responseChannels = (ArrayList)responseJSON["channels"];
                            List<String> validChannels = new List<String>();
                            foreach (Hashtable channel in responseChannels)
                            {
                                DiscordChannel builtChannel = null;
                                String ID = (String)channel["id"];
                                validChannels.Add(ID);
                                if (!Channels.TryGetValue(ID, out builtChannel))
                                {
                                    builtChannel = new DiscordChannel()
                                    {
                                        ID = ID
                                    };
                                    Channels[ID] = builtChannel;
                                }
                                builtChannel.Name = (String)channel["name"];
                                builtChannel.Position = Int32.Parse(channel["position"].ToString());
                            }
                            //Remove all old channels
                            List<String> removeChannelIDs = Channels.Keys.Where(ID => !validChannels.Contains(ID)).ToList();
                            foreach (String removeID in removeChannelIDs)
                            {
                                Channels.Remove(removeID);
                            }
                            // Members
                            ArrayList responseMembers = (ArrayList)responseJSON["members"];
                            List<String> validMembers = new List<String>();
                            foreach (Hashtable member in responseMembers)
                            {
                                DiscordMember builtMember = null;
                                String ID = (String)member["id"];
                                if (!String.IsNullOrEmpty(ID))
                                {
                                    validMembers.Add(ID);
                                    if (!Members.TryGetValue(ID, out builtMember))
                                    {
                                        builtMember = new DiscordMember();
                                        builtMember.ID = ID;
                                        builtMember.Name = (String)member["username"];
                                        Members[builtMember.ID] = builtMember;
                                    }
                                    //username
                                    if (member.ContainsKey("username"))
                                    {
                                        builtMember.Name = (String)member["username"];
                                    }
                                    // Player Object
                                    if (!builtMember.PlayerTested &&
                                        _plugin._threadsReady &&
                                        _plugin._firstPlayerListComplete &&
                                        !_plugin._databaseConnectionCriticalState)
                                    {
                                        builtMember.PlayerTested = true;
                                        builtMember.PlayerObject = _plugin.FetchPlayer(false, false, false, null, -1, null, null, null, builtMember.ID);
                                        // Do not accept memory-only players, only those with real IDs
                                        if (builtMember.PlayerObject != null && builtMember.PlayerObject.player_id <= 0)
                                        {
                                            builtMember.PlayerObject = null;
                                        }
                                        if (builtMember.PlayerObject != null && DebugMembers)
                                        {
                                            _plugin.Log.Info("Discord member " + builtMember.Name + " loaded with link to " + builtMember.PlayerObject.GetVerboseName());
                                        }
                                    }
                                    // Update their last usage time so they aren't purged from memory
                                    if (builtMember.PlayerObject != null)
                                    {
                                        builtMember.PlayerObject.LastUsage = _plugin.UtcNow();
                                    }
                                    //nick
                                    if (member.ContainsKey("nick"))
                                    {
                                        // Replace their username with their nickname, since that's what client's see
                                        builtMember.Name = (String)member["nick"];
                                    }
                                    //status
                                    if (member.ContainsKey("status"))
                                    {
                                        builtMember.Status = (String)member["status"];
                                    }
                                    //bot
                                    if (member.ContainsKey("bot"))
                                    {
                                        builtMember.Bot = (Boolean)member["bot"];
                                    }
                                    else
                                    {
                                        builtMember.Bot = false;
                                    }
                                    //channel_id
                                    if (member.ContainsKey("channel_id"))
                                    {
                                        Channels.TryGetValue((String)member["channel_id"], out builtMember.Channel);
                                    }
                                    else
                                    {
                                        builtMember.Channel = null;
                                    }
                                    //game
                                    if (member.ContainsKey("game"))
                                    {
                                        DiscordGame builtGame = null;
                                        Hashtable responseGame = (Hashtable)member["game"];
                                        if (responseGame.ContainsKey("name"))
                                        {
                                            builtGame = new DiscordGame();
                                            builtGame.Name = (String)responseGame["name"];
                                        }
                                        builtMember.Game = builtGame;
                                    }
                                    else
                                    {
                                        builtMember.Game = null;
                                    }
                                    //mute
                                    if (member.ContainsKey("mute"))
                                    {
                                        builtMember.Mute = (Boolean)member["mute"];
                                    }
                                    else
                                    {
                                        builtMember.Mute = false;
                                    }
                                    //self_mute
                                    if (member.ContainsKey("self_mute"))
                                    {
                                        builtMember.SelfMute = (Boolean)member["self_mute"];
                                    }
                                    else
                                    {
                                        builtMember.SelfMute = false;
                                    }
                                    //suppress
                                    if (member.ContainsKey("suppress"))
                                    {
                                        builtMember.Suppress = (Boolean)member["suppress"];
                                    }
                                    else
                                    {
                                        builtMember.Suppress = false;
                                    }
                                    //deaf
                                    if (member.ContainsKey("deaf"))
                                    {
                                        builtMember.Deaf = (Boolean)member["deaf"];
                                    }
                                    else
                                    {
                                        builtMember.Deaf = false;
                                    }
                                    //self_deaf
                                    if (member.ContainsKey("self_deaf"))
                                    {
                                        builtMember.SelfDeaf = (Boolean)member["self_deaf"];
                                    }
                                    else
                                    {
                                        builtMember.SelfDeaf = false;
                                    }
                                    //avatar_url
                                    if (member.ContainsKey("avatar_url"))
                                    {
                                        builtMember.AvatarURL = (String)member["avatar_url"];
                                    }
                                    else
                                    {
                                        builtMember.AvatarURL = null;
                                    }
                                    //avatar
                                    if (member.ContainsKey("avatar"))
                                    {
                                        builtMember.Avatar = (String)member["avatar"];
                                    }
                                    else
                                    {
                                        builtMember.Avatar = null;
                                    }
                                    //discriminator
                                    if (member.ContainsKey("discriminator"))
                                    {
                                        builtMember.Discriminator = (String)member["discriminator"];
                                    }
                                    else
                                    {
                                        builtMember.Discriminator = null;
                                    }
                                }
                            }
                            //Remove all old channels
                            List<String> removeMemberIDs = Members.Keys.ToList().Where(ID => !String.IsNullOrEmpty(ID) && !validMembers.Contains(ID)).ToList();
                            foreach (String removeID in removeMemberIDs)
                            {
                                Members.Remove(removeID);
                            }
                            LastUpdate = _plugin.UtcNow();
                            success = true;
                            ConnectionIssueCount = 0;
                        }
                        catch (Exception e)
                        {
                            if (e is WebException)
                            {
                                if (++ConnectionIssueCount > 3)
                                {
                                    _plugin.Log.Warn("Issue connecting to discord widget URL (" + ConnectionIssueCount + "): " + widgetURL);
                                }
                            }
                            else
                            {
                                _plugin.Log.HandleException(new AException("Error while parsing discord widget data.", e));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error while updating discord server information.", e));
                }
                _plugin.Log.Debug(() => "Exiting UpdateDiscordServerInfo", 7);
                return success;
            }

            public String GetWidgetURL()
            {
                if (String.IsNullOrEmpty(ServerID))
                {
                    _plugin.Log.Error("Cannot get discord widget URL, no server ID provided.");
                    return null;
                }
                return APIUrl + "api/servers/" + ServerID + "/widget.json";
            }

            public class DiscordMember
            {
                public String ID;
                public String Name;
                public String Nick;
                public String Status;
                public DiscordChannel Channel;
                public DiscordGame Game;
                public Boolean Bot;
                public Boolean Mute;
                public Boolean SelfMute;
                public Boolean Suppress;
                public Boolean Deaf;
                public Boolean SelfDeaf;
                public String AvatarURL;
                public String Avatar;
                public String Discriminator;
                public Boolean PlayerTested;
                public APlayer PlayerObject;
            }

            public class DiscordGame
            {
                public String Name;
            }

            public class DiscordChannel
            {
                public String ID;
                public String Name;
                public Int32 Position;
            }

            // Thanks to jbrunink for this code snippet
            public void PostReport(ARecord record, String type, String sourceInfo, String targetInfo)
            {
                if (record.target_player == null)
                {
                    _plugin.SendMessageToSource(record, "Unable to send report. No target player found.");
                    return;
                }
                var debugString = "Sending Discord report [" + record.command_numeric + "] on " + record.GetTargetNames();
                _plugin.Log.Debug(() => debugString, 3);
                if (_plugin._UseExperimentalTools)
                {
                    _plugin.Log.Info(debugString);
                }

                String blockOpener = "```" + Environment.NewLine;
                String blockCloser = "```";

                StringBuilder bb = new StringBuilder();
                bb.Append(blockOpener);
                bb.Append(_plugin.GameVersion + " " + type + " [" + record.command_numeric + "]" + Environment.NewLine);
                bb.Append("Source: " + sourceInfo + Environment.NewLine);
                bb.Append("Target: " + targetInfo + Environment.NewLine);
                bb.Append("Reason: " + record.record_message);
                bb.Append(Environment.NewLine);
                bb.Append(record.GetTargetNames() +
                       " rank(" + record.target_player.fbpInfo.Rank +
                    "), score(" + record.target_player.fbpInfo.Score +
                    "), kills(" + record.target_player.fbpInfo.Kills +
                    "), deaths(" + record.target_player.fbpInfo.Deaths +
                    "), k/d(" + Math.Round(record.target_player.fbpInfo.Kdr, 1) + ")" + Environment.NewLine);
                bb.Append(blockCloser);
                String body = bb.ToString();

                PostReportToDiscord(body + GetMentionString(RoleIDsToMentionReport));
            }

            public Hashtable GetEmbed()
            {
                return new Hashtable {
                    { "timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                    { "author", new Hashtable { { "name", "AdKats"}, {"icon_url", "https://avatars1.githubusercontent.com/u/9680130"}}}
                };
            }

            public String GetMentionString(List<String> roles)
            {
                String mentions = "";
                foreach (var role in roles)
                {
                    if (String.IsNullOrEmpty(role))
                        continue;
                    mentions += "<@&" + role + ">";
                }
                return mentions;
            }

            public void PostReportToDiscord(String body)
            {
                // the names of both PostReport functions are a little bit confugsion :)
                PostToWebhook(ReportWebhookUrl, body, null);
            }

            public void PostWatchListToDiscord(APlayer aPlayer, bool isJoin, String joinLocation)
            {
                // Build the watchlist embed and send it
                var embed = GetEmbed();
                if (isJoin)
                {
                    embed["title"] = "Watchlist Join Alert";
                    embed["description"] = "**" + aPlayer.GetVerboseName() + "** has joined the server **" + _plugin._shortServerName + "** as a **" + joinLocation + "**.";
                    embed["color"] = 0xFF0000; // Red
                }
                else
                {
                    embed["title"] = "Watchlist Leave Alert";
                    embed["description"] = "**" + aPlayer.GetVerboseName() + "** has left the server **" + _plugin._shortServerName + "**.";
                    embed["color"] = 0x00FF00; // Green
                }

                embed["fields"] = new ArrayList {
                    new Hashtable {
                        { "name", "Name"},
                        { "value", aPlayer.GetVerboseName()},
                        { "inline", true}
                    },
                    new Hashtable {
                        { "name", "Event"},
                        { "value", (isJoin ? "Server Join" : "Server Leave")},
                        { "inline", true}
                    },
                    new Hashtable {
                        { "name", "Server"},
                        { "value", _plugin._shortServerName},
                        { "inline", true}
                    },
                };

                PostToWebhook(WatchlistWebhookUrl, (isJoin ? GetMentionString(RoleIDsToMentionWatchlist) : ""), embed);
            }

            public void PostToWebhook(String url, String content, Hashtable embed)
            {
                try
                {
                    if (String.IsNullOrEmpty(url))
                    {
                        _plugin.Log.Error("Discord WebHook URL empty! Unable to post announcement. Check the URL settings!");
                        return;
                    }
                    if (String.IsNullOrEmpty(_plugin._shortServerName))
                    {
                        _plugin.Log.Error("The 'Short Server Name' setting must be filled in before posting watchlist announcements to discord.");
                        return;
                    }

                    // add embed to embeds if set
                    ArrayList embeds = new ArrayList();
                    if (embed != null)
                        embeds.Add(embed);

                    WebRequest request = WebRequest.Create(url);
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    String jsonBody = JSON.JsonEncode(new Hashtable {
                        {"avatar_url", "https://avatars1.githubusercontent.com/u/9680130"},
                        {"username", "AdKats (" + _plugin._shortServerName + ")"},
                        {"content", content},
                        {"embeds", embeds}
                    });
                    byte[] byteArray = Encoding.UTF8.GetBytes(jsonBody);
                    request.ContentLength = byteArray.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(byteArray, 0, byteArray.Length);
                    requestStream.Close();
                }
                catch (WebException e)
                {
                    using (WebResponse response = e.Response)
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        _plugin.Log.Info("RESPONSE: " + reader.ReadToEnd());
                    }
                    _plugin.Log.HandleException(new AException("Web error posting to Discord WebHook.", e));
                }
                catch (Exception e)
                {
                    _plugin.Log.HandleException(new AException("Error posting to Discord WebHook.", e));
                }
            }
        }

        //Directly pulled from TeamSpeak3Sync and adapted into inline library
        public class TeamSpeakClientViewer
        {
            public TeamSpeakClientViewer(AdKats plugin)
            {
                _plugin = plugin;

                try
                {
                    Thread mThreadMain = new Thread(EntryMain);
                    Thread mThreadSynchronize = new Thread(EntrySynchronization);
                    mThreadMain.Start();
                    mThreadSynchronize.Start();
                }
                catch (Exception e)
                {
                    ConsoleWrite("^8A fatal error occurred on load! Procon must be restarted for TSCV to work correctly.");
                    ConsoleWrite("^8^bMessage:^n^0 " + e.Message);
                    ConsoleWrite("^8^bStack Trace:^n^0 " + e.StackTrace);
                }

                Ts3QueryNickname = "TeamSpeakClientViewer";
                Ts3SubChannelNames = new String[] { };
            }

            public Boolean Enabled()
            {
                return _mEnabled;
            }

            public void Enable()
            {
                ConsoleWrite("^2^bTSCV credit to Teamspeak3Sync by Imisnew2^n");
                ConsoleWrite("[Enabled] ^2^bRequesting TSCV start...^n");
                AddToActionQueue(Commands.ClientEnabled);
            }

            public void Disable()
            {
                ConsoleWrite("[Disabled] ^8^bRequesting TSCV stop...^n");
                AddToActionQueue(Commands.ClientDisabled);
                if (_mTsReconnecting)
                {
                    _mTsReconnEvent.Set();
                }
            }

            public List<TeamspeakClient> GetPlayersOnTs()
            {
                return _mClientTsInfo.ToList();
            }

            private readonly AdKats _plugin;

            private Boolean _mEnabled;

            public String Ts3ServerIp
            {
                get; set;
            }
            public UInt16 Ts3ServerPort
            {
                get; set;
            }
            public UInt16 Ts3QueryPort
            {
                get; set;
            }
            public String Ts3QueryUsername
            {
                get; set;
            }
            public String Ts3QueryPassword
            {
                get; set;
            }
            public String Ts3QueryNickname
            {
                get; set;
            }
            public String Ts3MainChannelName
            {
                get; set;
            }
            public String[] Ts3SubChannelNames
            {
                get; set;
            }
            public Boolean DebugClients
            {
                get; set;
            }
            private Object _teamspeakLocker = new Object();

            public VoipJoinDisplayType JoinDisplay = VoipJoinDisplayType.Disabled;
            public String JoinDisplayMessage = "%playerusername% joined teamspeak! Welcome!";
            public Int32 UpdateIntervalSeconds = 30;

            private const Int32 SynDelayQueriesAmount = 1000;
            private const Int32 ErrReconnectOnErrorAttempts = 20;
            private const Int32 ErrReconnectOnErrorInterval = 30000;

            public enum Commands
            {
                ClientEnabled,
                ClientDisabled,
                UpdateTsClientInfo,
            }

            public enum Queries
            {
                OpenConnectionEstablish,
                OpenConnectionLogin,
                OpenConnectionUse,
                OpenConnectionMain,
                OpenConnectionNickname,

                TsInfoClientList,
                TsInfoChannelList,
                TsInfoClientInfo
            }

            private readonly Mutex _mActionMutex = new Mutex();
            private readonly Semaphore _mActionSemaphore = new Semaphore(0, Int32.MaxValue);
            private Queue<ActionEvent> _mActions = new Queue<ActionEvent>();
            private readonly TeamspeakConnection _mTsConnection = new TeamspeakConnection();
            private TeamspeakResponse _mTsResponse = new TeamspeakResponse("error id=0 msg=ok");
            private Boolean _mTsReconnecting;
            private DateTime _mTsPrevSendTime = DateTime.Now;
            private readonly AutoResetEvent _mTsReconnEvent = new AutoResetEvent(false);
            private List<TeamspeakClient> _mClientTsInfo = new List<TeamspeakClient>();
            private readonly TeamspeakChannel _mMainChannel = new TeamspeakChannel();
            private readonly List<TeamspeakChannel> _mPickupChannels = new List<TeamspeakChannel>();
            private ActionEvent _mCurrentAction;

            public class TeamspeakConnection
            {
                private static readonly TeamspeakResponse TsrOk = new TeamspeakResponse("error id=0 msg=ok");
                private static readonly TeamspeakResponse TsrOpenErr1 = new TeamspeakResponse("error id=-1 msg=The\\sconnection\\swas\\sreopened\\swhen\\sthe\\sprevious\\sconnection\\swas\\sstill\\sopen.");
                private static readonly TeamspeakResponse TsrOpenErr2 = new TeamspeakResponse("error id=-2 msg=Invalid\\sIP\\sAddress.");
                private static readonly TeamspeakResponse TsrOpenErr3 = new TeamspeakResponse("error id=-3 msg=Invalid\\sPort.");
                private static readonly TeamspeakResponse TsrOpenErr4 = new TeamspeakResponse("error id=-4 msg=An\\serror\\soccurred\\swhen\\strying\\sto\\sestablish\\sa\\sconnection.");
                private static readonly TeamspeakResponse TsrSendErr1 = new TeamspeakResponse("error id=-5 msg=The\\sconnection\\swas\\sclosed\\swhen\\sa\\squery\\swas\\stried\\sto\\sbe\\ssent.");
                private static readonly TeamspeakResponse TsrSendErr2 = new TeamspeakResponse("error id=-6 msg=The\\squery\\sto\\sbe\\ssent\\swas\\snull.");
                private static readonly TeamspeakResponse TsrSendErr3 = new TeamspeakResponse("error id=-7 msg=An\\serror\\soccurred\\swhen\\sthe\\squery\\swas\\ssent.");
                private static readonly TeamspeakResponse TsrSendErr4 = new TeamspeakResponse("error id=-8 msg=An\\serror\\soccurred\\swhen\\sthe\\sresponse\\swas\\sreceived.");
                public Socket Socket;

                public TeamspeakConnection()
                {
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        SendTimeout = 5000,
                        ReceiveTimeout = 5000
                    };
                }

                public TeamspeakResponse Open(String ip, UInt16 port)
                {
                    if (Socket.Connected)
                    {
                        return TsrOpenErr1;
                    }
                    if (String.IsNullOrEmpty(ip))
                    {
                        return TsrOpenErr2;
                    }
                    if (port == 0)
                    {
                        return TsrOpenErr3;
                    }

                    String rBuffer = String.Empty;
                    Byte[] sBuffer = new Byte[2048];
                    try
                    {
                        Socket.Connect(ip, port);

                        Thread.Sleep(1000);
                        Int32 size = Socket.Receive(sBuffer, sBuffer.Length, SocketFlags.None);
                        rBuffer += Encoding.Default.GetString(sBuffer, 0, size);

                        if (!rBuffer.Contains("TS3"))
                        {
                            throw new Exception();
                        }
                    }
                    catch (Exception)
                    {
                        Close();
                        return TsrOpenErr4;
                    }
                    OnDataReceived(rBuffer);

                    return rBuffer.Contains("error id=") ? new TeamspeakResponse(rBuffer) : TsrOk;
                }

                public TeamspeakResponse Close()
                {
                    Socket.Close();
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                    {
                        SendTimeout = 5000,
                        ReceiveTimeout = 5000
                    };
                    return TsrOk;
                }

                public TeamspeakResponse Send(TeamspeakQuery query)
                {
                    if (!Socket.Connected)
                    {
                        return TsrSendErr1;
                    }
                    if (query == null)
                    {
                        return TsrSendErr2;
                    }

                    String rBuffer = null;
                    Byte[] sBuffer = null;

                    try
                    {
                        rBuffer = query.RawQuery();
                        sBuffer = Encoding.Default.GetBytes(rBuffer);
                        Socket.Send(sBuffer, rBuffer.Length, SocketFlags.None);
                    }
                    catch (Exception)
                    {
                        Close();
                        return TsrSendErr3;
                    }
                    OnDataSent(rBuffer);

                    rBuffer = String.Empty;
                    sBuffer = new Byte[65536];
                    DateTime start = DateTime.Now;
                    while (!rBuffer.Contains("error id=") || !rBuffer.EndsWith("\n\r"))
                    {
                        try
                        {
                            Int32 size = Socket.Receive(sBuffer, sBuffer.Length, SocketFlags.None);
                            rBuffer += Encoding.Default.GetString(sBuffer, 0, size);
                            if ((DateTime.Now - start).TotalMilliseconds > 5500)
                            {
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            Close();
                            return TsrSendErr4;
                        }
                    }
                    OnDataReceived(rBuffer);

                    return new TeamspeakResponse(rBuffer);
                }

                public delegate void DataHandler(String data);

                public event DataHandler DataSent;
                public event DataHandler DataReceived;

                private void OnDataSent(String data)
                {
                    if (DataSent != null)
                    {
                        DataSent(data.Trim());
                    }
                }

                private void OnDataReceived(String data)
                {
                    if (DataReceived != null)
                    {
                        DataReceived(data.Trim());
                    }
                }
            }

            public class TeamspeakResponse
            {
                private String _tsRaw;
                private TeamspeakResponseGroup _tsError;
                private List<TeamspeakResponseSection> _tsSections;

                public String RawResponse
                {
                    get
                    {
                        return _tsRaw;
                    }
                }

                public String Id
                {
                    get
                    {
                        return _tsError["id"];
                    }
                }

                public String Message
                {
                    get
                    {
                        return _tsError["msg"];
                    }
                }

                public String ExtraMessage
                {
                    get
                    {
                        return _tsError["extra_msg"];
                    }
                }

                public Boolean HasSections
                {
                    get
                    {
                        return _tsSections.Count != 0;
                    }
                }

                public ReadOnlyCollection<TeamspeakResponseSection> Sections
                {
                    get
                    {
                        return _tsSections.AsReadOnly();
                    }
                }

                public TeamspeakResponse(String rawResponse)
                {
                    Parse(rawResponse);
                }

                private void Parse(string raw)
                {
                    _tsRaw = raw.Replace("\n", @"\n").Replace("\r", @"\r");
                    _tsError = new TeamspeakResponseGroup("empty");
                    _tsSections = new List<TeamspeakResponseSection>();

                    foreach (String section in raw.Replace("\n\r", "\n").Split('\n'))
                    {
                        if (section.Contains("error id="))
                        {
                            _tsError = new TeamspeakResponseGroup(section.Trim());
                        }
                        else if (!String.IsNullOrEmpty(section.Trim()))
                        {
                            _tsSections.Add(new TeamspeakResponseSection(section.Trim()));
                        }
                    }
                }
            }

            public class TeamspeakResponseSection
            {
                private String _tsRaw;
                private List<TeamspeakResponseGroup> _tsGroups = new List<TeamspeakResponseGroup>();

                public String RawSection
                {
                    get
                    {
                        return _tsRaw;
                    }
                }

                public Boolean HasGroups
                {
                    get
                    {
                        return _tsGroups.Count != 0;
                    }
                }

                public ReadOnlyCollection<TeamspeakResponseGroup> Groups
                {
                    get
                    {
                        return _tsGroups.AsReadOnly();
                    }
                }

                public TeamspeakResponseSection(String rawSection)
                {
                    Parse(rawSection);
                }

                private void Parse(String raw)
                {
                    _tsRaw = raw;
                    _tsGroups = new List<TeamspeakResponseGroup>();

                    foreach (String group in raw.Split('|'))
                    {
                        _tsGroups.Add(new TeamspeakResponseGroup(group.Trim()));
                    }
                }
            }

            public class TeamspeakResponseGroup
            {
                private String _tsRaw;
                private Dictionary<String, String> _tsPairs = new Dictionary<String, String>();

                public String RawGroup
                {
                    get
                    {
                        return _tsRaw;
                    }
                }

                public String this[String key]
                {
                    get
                    {
                        return (_tsPairs.ContainsKey(key)) ? _tsPairs[key] : null;
                    }
                }

                public TeamspeakResponseGroup(String rawGroup)
                {
                    Parse(rawGroup);
                }

                private void Parse(String raw)
                {
                    _tsRaw = raw;
                    _tsPairs = new Dictionary<String, String>();

                    foreach (string element in raw.Split(' '))
                    {
                        if (element.Contains("="))
                        {
                            String[] pair = element.Split('=');
                            if (_tsPairs.ContainsKey(pair[0]))
                            {
                                _tsPairs[pair[0]] = TeamspeakHelper.ts_UnescapeString(pair[1]);
                            }
                            else
                            {
                                _tsPairs.Add(pair[0], TeamspeakHelper.ts_UnescapeString(pair[1]));
                            }
                        }
                    }
                }
            }

            public class TeamspeakQuery
            {
                private readonly String tsCommand;
                private readonly Dictionary<String, String> tsParameters;
                private readonly List<String> tsOptions;

                public String Command
                {
                    get
                    {
                        return tsCommand;
                    }
                }

                public TeamspeakQuery(String command)
                {
                    tsCommand = command;
                    tsParameters = new Dictionary<String, String>();
                    tsOptions = new List<String>();
                }

                public void AddParameter(String key, String value)
                {
                    String tKey = key.Trim();
                    String tValue = value.Trim();
                    if (!String.IsNullOrEmpty(tKey) && !String.IsNullOrEmpty(tValue))
                    {
                        if (!tsParameters.ContainsKey(tKey))
                        {
                            tsParameters.Add(TeamspeakHelper.ts_EscapeString(tKey), TeamspeakHelper.ts_EscapeString(tValue));
                        }
                    }
                }

                public void AddOption(String option)
                {
                    String tOption = option.Trim();
                    if (!String.IsNullOrEmpty(tOption))
                    {
                        tsOptions.Add(TeamspeakHelper.ts_EscapeString(tOption));
                    }
                }

                public void RemoveParameter(String key)
                {
                    String tKey = key.Trim();
                    if (!String.IsNullOrEmpty(tKey))
                    {
                        tsParameters.Remove(tKey);
                    }
                }

                public void RemoveOption(String option)
                {
                    String tOption = option.Trim();
                    if (!String.IsNullOrEmpty(tOption))
                    {
                        tsOptions.Remove(tOption);
                    }
                }

                public String RawQuery()
                {
                    StringBuilder rawQuery = new StringBuilder();

                    rawQuery.Append(tsCommand);

                    foreach (KeyValuePair<String, String> p in tsParameters)
                    {
                        rawQuery.AppendFormat(" {0}={1}", p.Key, p.Value);
                    }

                    foreach (String o in tsOptions)
                    {
                        rawQuery.AppendFormat(" -{0}", o);
                    }

                    rawQuery.Append("\n");

                    return rawQuery.ToString();
                }

                public static TeamspeakQuery BuildLoginQuery(String username, String password)
                {
                    TeamspeakQuery tsLogin = new TeamspeakQuery("login");
                    tsLogin.AddParameter("client_login_name", username);
                    tsLogin.AddParameter("client_login_password", password);
                    return tsLogin;
                }

                public static TeamspeakQuery BuildChangeNicknameQuery(String newNickname)
                {
                    TeamspeakQuery tsClientUpdate = new TeamspeakQuery("clientupdate");
                    tsClientUpdate.AddParameter("client_nickname", newNickname);
                    return tsClientUpdate;
                }

                public static TeamspeakQuery BuildServerListQuery()
                {
                    return new TeamspeakQuery("serverlist");
                }

                public static TeamspeakQuery BuildUseVIdQuery(Int32 virtualId)
                {
                    TeamspeakQuery tsUse = new TeamspeakQuery("use");
                    tsUse.AddParameter("sid", virtualId.ToString(CultureInfo.InvariantCulture));
                    return tsUse;
                }

                public static TeamspeakQuery BuildUsePortQuery(Int32 port)
                {
                    TeamspeakQuery tsUse = new TeamspeakQuery("use");
                    tsUse.AddParameter("port", port.ToString(CultureInfo.InvariantCulture));
                    return tsUse;
                }

                public static TeamspeakQuery BuildChannelListQuery()
                {
                    return new TeamspeakQuery("channellist");
                }

                public static TeamspeakQuery BuildChannelFindQuery(String channelName)
                {
                    TeamspeakQuery tsChannelFind = new TeamspeakQuery("channelfind");
                    tsChannelFind.AddParameter("pattern", channelName);
                    return tsChannelFind;
                }

                public static TeamspeakQuery BuildChannelInfoQuery(Int32 channelId)
                {
                    TeamspeakQuery tsChannelInfo = new TeamspeakQuery("channelinfo");
                    tsChannelInfo.AddParameter("cid", channelId.ToString(CultureInfo.InvariantCulture));
                    return tsChannelInfo;
                }

                public static TeamspeakQuery BuildClientListQuery()
                {
                    return new TeamspeakQuery("clientlist");
                }

                public static TeamspeakQuery BuildClientFindQuery(String clientName)
                {
                    TeamspeakQuery tsClientFind = new TeamspeakQuery("clientfind");
                    tsClientFind.AddParameter("pattern", clientName);
                    return tsClientFind;
                }

                public static TeamspeakQuery BuildClientInfoQuery(Int32 clientId)
                {
                    TeamspeakQuery tsClientInfo = new TeamspeakQuery("clientinfo");
                    tsClientInfo.AddParameter("clid", clientId.ToString(CultureInfo.InvariantCulture));
                    return tsClientInfo;
                }

                public static TeamspeakQuery BuildClientMoveQuery(Int32 clientId, Int32 channelId)
                {
                    TeamspeakQuery tsClientMove = new TeamspeakQuery("clientmove");
                    tsClientMove.AddParameter("clid", clientId.ToString(CultureInfo.InvariantCulture));
                    tsClientMove.AddParameter("cid", channelId.ToString(CultureInfo.InvariantCulture));
                    return tsClientMove;
                }
            }

            public class TeamspeakServer
            {
                public String TsName = null; //virtualserver_name
                public Int32? TsId = null; //virtualserver_id
                public Int32? TsPort = null; //virtualserver_port
                public Int32? TsMachineId = null; //virtualserver_machine_id

                public String TsStatus = null; //virtualserver_status
                public Int32? TsUpTime = null; //virtualserver_uptime
                public Int32? TsClientsOnline = null; //virtualserver_clientsonline
                public Int32? TsQueryClientsOnline = null; //virtualserver_queryclientsonline

                public Int32? TsQueryMaxClients = null; //virtualserver_maxclients
                public Boolean? TsAutoStart = null; //virtualserver_autostart

                public TeamspeakServer()
                {
                }

                public TeamspeakServer(TeamspeakResponseGroup serverInfo)
                {
                    SetBasicData(serverInfo);
                }

                public void SetBasicData(TeamspeakResponseGroup serverInfo)
                {
                    String value;
                    Int32 iValue;
                    Boolean bValue;

                    TsName = serverInfo["virtualserver_name"];
                    if ((value = serverInfo["virtualserver_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsId = iValue;
                        }
                        else
                        {
                            TsId = null;
                        }
                    }
                    else
                    {
                        TsId = null;
                    }
                    if ((value = serverInfo["virtualserver_port"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsPort = iValue;
                        }
                        else
                        {
                            TsPort = null;
                        }
                    }
                    else
                    {
                        TsPort = null;
                    }
                    if ((value = serverInfo["virtualserver_machine_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsMachineId = iValue;
                        }
                        else
                        {
                            TsMachineId = null;
                        }
                    }
                    else
                    {
                        TsMachineId = null;
                    }

                    TsStatus = serverInfo["virtualserver_status"];
                    if ((value = serverInfo["virtualserver_uptime"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsUpTime = iValue;
                        }
                        else
                        {
                            TsUpTime = null;
                        }
                    }
                    else
                    {
                        TsUpTime = null;
                    }
                    if ((value = serverInfo["virtualserver_clientsonline"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsClientsOnline = iValue;
                        }
                        else
                        {
                            TsClientsOnline = null;
                        }
                    }
                    else
                    {
                        TsClientsOnline = null;
                    }
                    if ((value = serverInfo["virtualserver_queryclientsonline"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsQueryClientsOnline = iValue;
                        }
                        else
                        {
                            TsQueryClientsOnline = null;
                        }
                    }
                    else
                    {
                        TsQueryClientsOnline = null;
                    }

                    if ((value = serverInfo["virtualserver_maxclients"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsQueryMaxClients = iValue;
                        }
                        else
                        {
                            TsQueryMaxClients = null;
                        }
                    }
                    else
                    {
                        TsQueryMaxClients = null;
                    }
                    if ((value = serverInfo["virtualserver_autostart"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            TsAutoStart = bValue;
                        }
                        else
                        {
                            TsAutoStart = null;
                        }
                    }
                    else
                    {
                        TsAutoStart = null;
                    }
                }
            }

            public class TeamspeakChannel
            {
                public String TsName = null; //channel_name
                public Int32? TsId = null; //cid

                public Int32? MedPId = null; //pid
                public Int32? MedOrder = null; //channel_order
                public Int32? MedTotalClients = null; //total_clients
                public Int32? MedPowerNeededToSub = null; //channel_needed_subscribe_power

                public String AdvTopic = null; //channel_topic
                public String AdvDescription = null; //channel_description
                public String AdvPassword = null; //channel_password
                public String AdvFilepath = null; //channel_filepath
                public String AdvPhoneticName = null; //channel_name_phonetic
                public Int32? AdvCodec = null; //channel_codec
                public Int32? AdvCodecQuality = null; //channel_codec_quality
                public Int32? AdvCodecLatencyFactor = null; //channel_codec_latency_factor
                public Int32? AdvMaxClients = null; //channel_maxclients
                public Int32? AdvMaxFamilyClients = null; //channel_maxfamilyclients
                public Int32? AdvNeededTalkPower = null; //channel_needed_talk_power
                public Int32? AdvIconId = null; //channel_icon_id
                public Boolean? AdvFlagPermanent = null; //channel_flag_permanent
                public Boolean? AdvFlagSemiPermanent = null; //channel_flag_semi_permanent
                public Boolean? AdvFlagDefault = null; //channel_flag_default
                public Boolean? AdvFlagPassword = null; //channel_flag_password
                public Boolean? AdvFlagMaxClientsUnlimited = null; //channel_flag_maxclients_unlimited
                public Boolean? AdvFlagMaxFamilyClientsUnlimited = null; //channel_flag_maxfamilyclients_unlimited
                public Boolean? AdvFlagMaxFamilyClientsInherited = null; //channel_flag_maxfamilyclients_inherited
                public Boolean? AdvForcedSilence = null; //channel_forced_silence

                public TeamspeakChannel()
                {
                }

                public TeamspeakChannel(TeamspeakResponseGroup channelInfo)
                {
                    SetBasicData(channelInfo);
                    SetMediumData(channelInfo);
                    SetAdvancedData(channelInfo);
                }

                public void SetBasicData(TeamspeakResponseGroup channelInfo)
                {
                    String value;
                    Int32 iValue;

                    TsName = channelInfo["channel_name"];
                    if ((value = channelInfo["cid"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsId = iValue;
                        }
                        else
                        {
                            TsId = null;
                        }
                    }
                    else
                    {
                        TsId = null;
                    }
                }

                public void SetMediumData(TeamspeakResponseGroup channelInfo)
                {
                    String value;
                    Int32 iValue;

                    if ((value = channelInfo["pid"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedPId = iValue;
                        }
                        else
                        {
                            MedPId = null;
                        }
                    }
                    else
                    {
                        MedPId = null;
                    }
                    if ((value = channelInfo["channel_order"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedOrder = iValue;
                        }
                        else
                        {
                            MedOrder = null;
                        }
                    }
                    else
                    {
                        MedOrder = null;
                    }
                    if ((value = channelInfo["total_clients"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedTotalClients = iValue;
                        }
                        else
                        {
                            MedTotalClients = null;
                        }
                    }
                    else
                    {
                        MedTotalClients = null;
                    }
                    if ((value = channelInfo["channel_needed_subscribe_power"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedPowerNeededToSub = iValue;
                        }
                        else
                        {
                            MedPowerNeededToSub = null;
                        }
                    }
                    else
                    {
                        MedPowerNeededToSub = null;
                    }
                }

                public void SetAdvancedData(TeamspeakResponseGroup channelInfo)
                {
                    String value;
                    Int32 iValue;
                    Boolean bValue;

                    AdvTopic = channelInfo["channel_topic"];
                    AdvDescription = channelInfo["channel_description"];
                    AdvPassword = channelInfo["channel_password"];
                    AdvFilepath = channelInfo["channel_filepath"];
                    AdvPhoneticName = channelInfo["channel_name_phonetic"];
                    if ((value = channelInfo["channel_codec"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvCodec = iValue;
                        }
                        else
                        {
                            AdvCodec = null;
                        }
                    }
                    else
                    {
                        AdvCodec = null;
                    }
                    if ((value = channelInfo["channel_codec_quality"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvCodecQuality = iValue;
                        }
                        else
                        {
                            AdvCodecQuality = null;
                        }
                    }
                    else
                    {
                        AdvCodecQuality = null;
                    }
                    if ((value = channelInfo["channel_codec_latency_factor"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvCodecLatencyFactor = iValue;
                        }
                        else
                        {
                            AdvCodecLatencyFactor = null;
                        }
                    }
                    else
                    {
                        AdvCodecLatencyFactor = null;
                    }
                    if ((value = channelInfo["channel_maxclients"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvMaxClients = iValue;
                        }
                        else
                        {
                            AdvMaxClients = null;
                        }
                    }
                    else
                    {
                        AdvMaxClients = null;
                    }
                    if ((value = channelInfo["channel_maxfamilyclients"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvMaxFamilyClients = iValue;
                        }
                        else
                        {
                            AdvMaxFamilyClients = null;
                        }
                    }
                    else
                    {
                        AdvMaxFamilyClients = null;
                    }
                    if ((value = channelInfo["channel_needed_talk_power"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvNeededTalkPower = iValue;
                        }
                        else
                        {
                            AdvNeededTalkPower = null;
                        }
                    }
                    else
                    {
                        AdvNeededTalkPower = null;
                    }
                    if ((value = channelInfo["channel_icon_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvIconId = iValue;
                        }
                        else
                        {
                            AdvIconId = null;
                        }
                    }
                    else
                    {
                        AdvIconId = null;
                    }
                    if ((value = channelInfo["channel_flag_permanent"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagPermanent = bValue;
                        }
                        else
                        {
                            AdvFlagPermanent = null;
                        }
                    }
                    else
                    {
                        AdvFlagPermanent = null;
                    }
                    if ((value = channelInfo["channel_flag_semi_permanent"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagSemiPermanent = bValue;
                        }
                        else
                        {
                            AdvFlagSemiPermanent = null;
                        }
                    }
                    else
                    {
                        AdvFlagSemiPermanent = null;
                    }
                    if ((value = channelInfo["channel_flag_default"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagDefault = bValue;
                        }
                        else
                        {
                            AdvFlagDefault = null;
                        }
                    }
                    else
                    {
                        AdvFlagDefault = null;
                    }
                    if ((value = channelInfo["channel_flag_password"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagPassword = bValue;
                        }
                        else
                        {
                            AdvFlagPassword = null;
                        }
                    }
                    else
                    {
                        AdvFlagPassword = null;
                    }
                    if ((value = channelInfo["channel_flag_maxclients_unlimited"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagMaxClientsUnlimited = bValue;
                        }
                        else
                        {
                            AdvFlagMaxClientsUnlimited = null;
                        }
                    }
                    else
                    {
                        AdvFlagMaxClientsUnlimited = null;
                    }
                    if ((value = channelInfo["channel_flag_maxfamilyclients_unlimited"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagMaxFamilyClientsUnlimited = bValue;
                        }
                        else
                        {
                            AdvFlagMaxFamilyClientsUnlimited = null;
                        }
                    }
                    else
                    {
                        AdvFlagMaxFamilyClientsUnlimited = null;
                    }
                    if ((value = channelInfo["channel_flag_maxfamilyclients_inherited"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvFlagMaxFamilyClientsInherited = bValue;
                        }
                        else
                        {
                            AdvFlagMaxFamilyClientsInherited = null;
                        }
                    }
                    else
                    {
                        AdvFlagMaxFamilyClientsInherited = null;
                    }
                    if ((value = channelInfo["channel_forced_silence"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvForcedSilence = bValue;
                        }
                        else
                        {
                            AdvForcedSilence = null;
                        }
                    }
                    else
                    {
                        AdvForcedSilence = null;
                    }
                }
            }

            public class TeamspeakClient
            {
                public String TsName = null; //client_nickname
                public Int32? TsId = null; //clid

                public Int32? MedDatabaseId = null; //client_database_id
                public Int32? MedChannelId = null; //cid
                public String MedChannelName = null;
                public Int32? MedType = null; //client_type

                public String AdvLoginName = null; //client_login_name
                public String AdvUniqueId = null; //client_unique_identifier
                public String AdvIpAddress = null; //connection_client_ip

                public String AdvVersion = null; //client_version
                public String AdvPlatform = null; //client_platform
                public String AdvDescription = null; //client_description
                public String AdvCountry = null; //client_country
                public String AdvMetaData = null; //client_meta_data

                public Int32? AdvChannelGroupId = null; //client_channel_group_id
                public Int32? AdvServerGroupId = null; //client_servergroups
                public Boolean? AdvIsChannelCommander = null; //client_is_channel_commander

                public String AdvDefaultChannel = null; //client_default_channel
                public Int32? AdvConnectionTime = null; //connection_connected_time
                public Int32? AdvIdleTime = null; //client_idle_time
                public Int32? AdvCreationTime = null; //client_created
                public Int32? AdvLastConnected = null; //client_lastconnected
                public Int32? AdvTotalConnections = null; //client_totalconnections

                public Boolean? AdvInputMuted = null; //client_input_muted
                public Boolean? AdvOutputMuted = null; //client_output_muted
                public Boolean? AdvOutputMutedOnly = null; //client_outputonly_muted
                public Boolean? AdvInputHardware = null; //client_input_hardware
                public Boolean? AdvOutputHardware = null; //client_output_hardware
                public Boolean? AdvIsRecording = null; //client_is_recording

                public String AdvFlagAvatar = null; //client_flag_avatar
                public String AdvAwayMessage = null; //client_away_message
                public String AdvTalkMessage = null; //client_talk_request_msg
                public String AdvPhoneticNick = null; //client_nickname_phonetic
                public String AdvDefaultToken = null; //client_default_token
                public String AdvBase64Hash = null; //client_base64HashClientUID
                public Int32? AdvTalkPower = null; //client_talk_power
                public Int32? AdvQueryViewPower = null; //client_needed_serverquery_view_power
                public Int32? AdvUnreadMessages = null; //client_unread_messages
                public Int32? AdvIconId = null; //client_icon_id
                public Boolean? AdvIsAway = null; //client_away
                public Boolean? AdvTalkRequest = null; //client_talk_request
                public Boolean? AdvIsTalker = null; //client_is_talker
                public Boolean? AdvIsPriority = null; //client_is_priority_speaker

                public Int32? AdvBytesUpMonth = null; //client_month_bytes_uploaded
                public Int32? AdvBytesDownMonth = null; //client_month_bytes_downloaded
                public Int32? AdvBytesUpTotal = null; //client_total_bytes_uploaded
                public Int32? AdvBytesDownTotal = null; //client_total_bytes_downloaded
                public Int32? AdvFileBandwidthSent = null; //connection_filetransfer_bandwidth_sent
                public Int32? AdvFileBandwidthRec = null; //connection_filetransfer_bandwidth_received
                public Int32? AdvPacketsTotalSent = null; //connection_packets_sent_total
                public Int32? AdvPacketsTotalRec = null; //connection_packets_received_total
                public Int32? AdvBytesTotalSent = null; //connection_bytes_sent_total
                public Int32? AdvBytesTotalRec = null; //connection_bytes_received_total
                public Int32? AdvBndwdthSecondSent = null; //connection_bandwidth_sent_last_second_total
                public Int32? AdvBndwdthSecondRec = null; //connection_bandwidth_received_last_second_total
                public Int32? AdvBndwdthMinuteSent = null; //connection_bandwidth_sent_last_minute_total
                public Int32? AdvBndwdthMinuteRec = null; //connection_bandwidth_received_last_minute_total

                public TeamspeakClient()
                {
                }

                public TeamspeakClient(TeamspeakResponseGroup clientInfo)
                {
                    SetBasicData(clientInfo);
                    SetMediumData(clientInfo);
                    SetAdvancedData(clientInfo);
                }

                public void SetBasicData(TeamspeakResponseGroup clientInfo)
                {
                    String value;
                    Int32 iValue;

                    TsName = clientInfo["client_nickname"];
                    if ((value = clientInfo["clid"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            TsId = iValue;
                        }
                        else
                        {
                            TsId = null;
                        }
                    }
                    else
                    {
                        TsId = null;
                    }
                }

                public void SetMediumData(TeamspeakResponseGroup clientInfo)
                {
                    String value;
                    Int32 iValue;

                    if ((value = clientInfo["client_database_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedDatabaseId = iValue;
                        }
                        else
                        {
                            MedDatabaseId = null;
                        }
                    }
                    else
                    {
                        MedDatabaseId = null;
                    }
                    if ((value = clientInfo["cid"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedChannelId = iValue;
                        }
                        else
                        {
                            MedChannelId = null;
                        }
                    }
                    else
                    {
                        MedChannelId = null;
                    }
                    if ((value = clientInfo["client_type"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            MedType = iValue;
                        }
                        else
                        {
                            MedType = null;
                        }
                    }
                    else
                    {
                        MedType = null;
                    }
                }

                public void SetAdvancedData(TeamspeakResponseGroup clientInfo)
                {
                    String value;
                    Int32 iValue;
                    Boolean bValue;

                    AdvLoginName = clientInfo["client_login_name"];
                    AdvUniqueId = clientInfo["client_unique_identifier"];
                    AdvIpAddress = clientInfo["connection_client_ip"];

                    AdvVersion = clientInfo["client_version"];
                    AdvPlatform = clientInfo["client_platform"];
                    AdvDescription = clientInfo["client_description"];
                    AdvCountry = clientInfo["client_country"];
                    AdvMetaData = clientInfo["client_meta_data"];

                    if ((value = clientInfo["client_channel_group_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvChannelGroupId = iValue;
                        }
                        else
                        {
                            AdvChannelGroupId = null;
                        }
                    }
                    else
                    {
                        AdvChannelGroupId = null;
                    }
                    if ((value = clientInfo["client_servergroups"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvServerGroupId = iValue;
                        }
                        else
                        {
                            AdvServerGroupId = null;
                        }
                    }
                    else
                    {
                        AdvServerGroupId = null;
                    }
                    if ((value = clientInfo["client_is_channel_commander"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvIsChannelCommander = bValue;
                        }
                        else
                        {
                            AdvIsChannelCommander = null;
                        }
                    }
                    else
                    {
                        AdvIsChannelCommander = null;
                    }

                    AdvDefaultChannel = clientInfo["client_default_channel"];
                    if ((value = clientInfo["connection_connected_time"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvConnectionTime = iValue;
                        }
                        else
                        {
                            AdvConnectionTime = null;
                        }
                    }
                    else
                    {
                        AdvConnectionTime = null;
                    }
                    if ((value = clientInfo["client_idle_time"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvIdleTime = iValue;
                        }
                        else
                        {
                            AdvIdleTime = null;
                        }
                    }
                    else
                    {
                        AdvIdleTime = null;
                    }
                    if ((value = clientInfo["client_created"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvCreationTime = iValue;
                        }
                        else
                        {
                            AdvCreationTime = null;
                        }
                    }
                    else
                    {
                        AdvCreationTime = null;
                    }
                    if ((value = clientInfo["client_lastconnected"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvLastConnected = iValue;
                        }
                        else
                        {
                            AdvLastConnected = null;
                        }
                    }
                    else
                    {
                        AdvLastConnected = null;
                    }
                    if ((value = clientInfo["client_totalconnections"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvTotalConnections = iValue;
                        }
                        else
                        {
                            AdvTotalConnections = null;
                        }
                    }
                    else
                    {
                        AdvTotalConnections = null;
                    }

                    if ((value = clientInfo["client_input_muted"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvInputMuted = bValue;
                        }
                        else
                        {
                            AdvInputMuted = null;
                        }
                    }
                    else
                    {
                        AdvInputMuted = null;
                    }
                    if ((value = clientInfo["client_output_muted"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvOutputMuted = bValue;
                        }
                        else
                        {
                            AdvOutputMuted = null;
                        }
                    }
                    else
                    {
                        AdvOutputMuted = null;
                    }
                    if ((value = clientInfo["client_outputonly_muted"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvOutputMutedOnly = bValue;
                        }
                        else
                        {
                            AdvOutputMutedOnly = null;
                        }
                    }
                    else
                    {
                        AdvOutputMutedOnly = null;
                    }
                    if ((value = clientInfo["client_input_hardware"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvInputHardware = bValue;
                        }
                        else
                        {
                            AdvInputHardware = null;
                        }
                    }
                    else
                    {
                        AdvInputHardware = null;
                    }
                    if ((value = clientInfo["client_output_hardware"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvOutputHardware = bValue;
                        }
                        else
                        {
                            AdvOutputHardware = null;
                        }
                    }
                    else
                    {
                        AdvOutputHardware = null;
                    }
                    if ((value = clientInfo["client_is_recording"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvIsRecording = bValue;
                        }
                        else
                        {
                            AdvIsRecording = null;
                        }
                    }
                    else
                    {
                        AdvIsRecording = null;
                    }

                    AdvFlagAvatar = clientInfo["client_flag_avatar"];
                    AdvAwayMessage = clientInfo["client_away_message"];
                    AdvTalkMessage = clientInfo["client_talke_request_msg"];
                    AdvPhoneticNick = clientInfo["client_nickname_phonetic"];
                    AdvDefaultToken = clientInfo["client_default_token"];
                    AdvBase64Hash = clientInfo["client_base64HashClientUID"];
                    if ((value = clientInfo["client_talk_power"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvTalkPower = iValue;
                        }
                        else
                        {
                            AdvTalkPower = null;
                        }
                    }
                    else
                    {
                        AdvTalkPower = null;
                    }
                    if ((value = clientInfo["client_needed_serverquery_view_power"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvQueryViewPower = iValue;
                        }
                        else
                        {
                            AdvQueryViewPower = null;
                        }
                    }
                    else
                    {
                        AdvQueryViewPower = null;
                    }
                    if ((value = clientInfo["client_unread_messages"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvUnreadMessages = iValue;
                        }
                        else
                        {
                            AdvUnreadMessages = null;
                        }
                    }
                    else
                    {
                        AdvUnreadMessages = null;
                    }
                    if ((value = clientInfo["client_icon_id"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvIconId = iValue;
                        }
                        else
                        {
                            AdvIconId = null;
                        }
                    }
                    else
                    {
                        AdvIconId = null;
                    }
                    if ((value = clientInfo["client_away"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvIsAway = bValue;
                        }
                        else
                        {
                            AdvIsAway = null;
                        }
                    }
                    else
                    {
                        AdvIsAway = null;
                    }
                    if ((value = clientInfo["client_talk_request"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvTalkRequest = bValue;
                        }
                        else
                        {
                            AdvTalkRequest = null;
                        }
                    }
                    else
                    {
                        AdvTalkRequest = null;
                    }
                    if ((value = clientInfo["client_is_talker"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvIsTalker = bValue;
                        }
                        else
                        {
                            AdvIsTalker = null;
                        }
                    }
                    else
                    {
                        AdvIsTalker = null;
                    }
                    if ((value = clientInfo["client_is_priority_speaker"]) != null)
                    {
                        if (Boolean.TryParse(value, out bValue))
                        {
                            AdvIsPriority = bValue;
                        }
                        else
                        {
                            AdvIsPriority = null;
                        }
                    }
                    else
                    {
                        AdvIsPriority = null;
                    }

                    if ((value = clientInfo["client_month_bytes_uploaded"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesUpMonth = iValue;
                        }
                        else
                        {
                            AdvBytesUpMonth = null;
                        }
                    }
                    else
                    {
                        AdvBytesUpMonth = null;
                    }
                    if ((value = clientInfo["client_month_bytes_downloaded"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesDownMonth = iValue;
                        }
                        else
                        {
                            AdvBytesDownMonth = null;
                        }
                    }
                    else
                    {
                        AdvBytesDownMonth = null;
                    }
                    if ((value = clientInfo["client_total_bytes_uploaded"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesUpTotal = iValue;
                        }
                        else
                        {
                            AdvBytesUpTotal = null;
                        }
                    }
                    else
                    {
                        AdvBytesUpTotal = null;
                    }
                    if ((value = clientInfo["client_total_bytes_downloaded"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesDownTotal = iValue;
                        }
                        else
                        {
                            AdvBytesDownTotal = null;
                        }
                    }
                    else
                    {
                        AdvBytesDownTotal = null;
                    }
                    if ((value = clientInfo["connection_filetransfer_bandwidth_sent"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvFileBandwidthSent = iValue;
                        }
                        else
                        {
                            AdvFileBandwidthSent = null;
                        }
                    }
                    else
                    {
                        AdvFileBandwidthSent = null;
                    }
                    if ((value = clientInfo["connection_filetransfer_bandwidth_received"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvFileBandwidthRec = iValue;
                        }
                        else
                        {
                            AdvFileBandwidthRec = null;
                        }
                    }
                    else
                    {
                        AdvFileBandwidthRec = null;
                    }
                    if ((value = clientInfo["connection_packets_sent_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvPacketsTotalSent = iValue;
                        }
                        else
                        {
                            AdvUnreadMessages = null;
                        }
                    }
                    else
                    {
                        AdvUnreadMessages = null;
                    }
                    if ((value = clientInfo["connection_packets_received_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvPacketsTotalSent = iValue;
                        }
                        else
                        {
                            AdvPacketsTotalSent = null;
                        }
                    }
                    else
                    {
                        AdvPacketsTotalSent = null;
                    }
                    if ((value = clientInfo["connection_bytes_sent_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesTotalSent = iValue;
                        }
                        else
                        {
                            AdvBytesTotalSent = null;
                        }
                    }
                    else
                    {
                        AdvBytesTotalSent = null;
                    }
                    if ((value = clientInfo["connection_bytes_received_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBytesTotalRec = iValue;
                        }
                        else
                        {
                            AdvBytesTotalRec = null;
                        }
                    }
                    else
                    {
                        AdvBytesTotalRec = null;
                    }
                    if ((value = clientInfo["connection_bandwidth_sent_last_second_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBndwdthSecondSent = iValue;
                        }
                        else
                        {
                            AdvBndwdthSecondSent = null;
                        }
                    }
                    else
                    {
                        AdvBndwdthSecondSent = null;
                    }
                    if ((value = clientInfo["connection_bandwidth_received_last_second_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBndwdthSecondRec = iValue;
                        }
                        else
                        {
                            AdvBndwdthSecondRec = null;
                        }
                    }
                    else
                    {
                        AdvBndwdthSecondRec = null;
                    }
                    if ((value = clientInfo["connection_bandwidth_sent_last_minute_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBndwdthMinuteSent = iValue;
                        }
                        else
                        {
                            AdvBndwdthMinuteSent = null;
                        }
                    }
                    else
                    {
                        AdvBndwdthMinuteSent = null;
                    }
                    if ((value = clientInfo["connection_bandwidth_received_last_minute_total"]) != null)
                    {
                        if (Int32.TryParse(value, out iValue))
                        {
                            AdvBndwdthMinuteRec = iValue;
                        }
                        else
                        {
                            AdvBndwdthMinuteRec = null;
                        }
                    }
                    else
                    {
                        AdvBndwdthMinuteRec = null;
                    }
                }
            }

            private static class TeamspeakHelper
            {
                public static String ts_EscapeString(String text)
                {
                    String escaped = text.Replace("\\", @"\\");
                    escaped = escaped.Replace("/", @"\/");
                    escaped = escaped.Replace(" ", @"\s");
                    escaped = escaped.Replace("|", @"\p");
                    escaped = escaped.Replace("\a", @"\a");
                    escaped = escaped.Replace("\b", @"\b");
                    escaped = escaped.Replace("\f", @"\f");
                    escaped = escaped.Replace("\n", @"\n");
                    escaped = escaped.Replace("\r", @"\r");
                    escaped = escaped.Replace("\t", @"\t");
                    escaped = escaped.Replace("\v", @"\v");
                    return escaped;
                }

                public static String ts_UnescapeString(String text)
                {
                    String unescaped = text.Replace(@"\\", "\\");
                    unescaped = unescaped.Replace(@"\/", "/");
                    unescaped = unescaped.Replace(@"\s", " ");
                    unescaped = unescaped.Replace(@"\p", "|");
                    unescaped = unescaped.Replace(@"\a", "\a");
                    unescaped = unescaped.Replace(@"\b", "\b");
                    unescaped = unescaped.Replace(@"\f", "\f");
                    unescaped = unescaped.Replace(@"\n", "\n");
                    unescaped = unescaped.Replace(@"\r", "\r");
                    unescaped = unescaped.Replace(@"\t", "\t");
                    unescaped = unescaped.Replace(@"\v", "\v");
                    return unescaped;
                }
            }

            public class ActionEvent
            {
                private readonly Commands command = 0;
                private Int32 _argsIndex;
                private readonly List<Object> args = new List<Object>();

                public Commands Command
                {
                    get
                    {
                        return command;
                    }
                }

                public Object Argument
                {
                    get
                    {
                        return args[(_argsIndex == args.Count) ? (_argsIndex = 1) - 1 : _argsIndex++];
                    }
                }

                public ActionEvent(Commands command, Object[] args)
                {
                    this.command = command;
                    foreach (Object arg in args)
                    {
                        this.args.Add(arg);
                    }
                }
            }

            private void SetPluginState(Boolean state)
            {
                if (state)
                {
                    Enable();
                }
                else
                {
                    Disable();
                }
            }

            private void ConsoleWrite(String message, params Object[] args)
            {
                _plugin.ExecuteCommand("procon.protected.pluginconsole.write", String.Format("[TSCV] " + message, args));
            }

            private void DebugWrite(Boolean debug, String message, params Object[] args)
            {
                if (debug)
                {
                    ConsoleWrite(message, args);
                }
            }

            private void DataSent(String data)
            {
            }

            private void DataReceived(String data)
            {
            }

            private void EntryMain()
            {
                _mTsConnection.DataSent += DataSent;
                _mTsConnection.DataReceived += DataReceived;

                while (true)
                {
                    _mActionSemaphore.WaitOne();
                    _mActionMutex.WaitOne();
                    _mCurrentAction = _mActions.Dequeue();
                    _mActionMutex.ReleaseMutex();

                    // If we are disabled, and the incoming command can't change that, skip processing
                    if (!_mEnabled && _mCurrentAction.Command != Commands.ClientEnabled && _mCurrentAction.Command != Commands.ClientDisabled)
                    {
                        continue;
                    }

                    try
                    {
                        switch (_mCurrentAction.Command)
                        {
                            case Commands.ClientEnabled:
                                PerformOpenConnection();
                                break;
                            case Commands.ClientDisabled:
                                PerformCloseConnection();
                                break;
                            case Commands.UpdateTsClientInfo:
                                UpdateTsInfo();
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleWrite("^8A fatal error occurred during processing a command!");
                        ConsoleWrite("^8^bMessage:^n^0 " + e.Message);
                        ConsoleWrite("^8^bStack Trace:^n^0 " + e.StackTrace);
                        SetPluginState(false);
                    }
                }
            }

            private void EntrySynchronization()
            {
                while (true)
                {
                    if (_mEnabled && !_mTsReconnecting)
                    {
                        AddToActionQueue(Commands.UpdateTsClientInfo);
                    }
                    Thread.Sleep(UpdateIntervalSeconds * 1000);
                }
            }

            private void PerformOpenConnection()
            {
                for (int secondsSlept = 0; secondsSlept < 10 && Ts3ServerIp == "Teamspeak Ip"; secondsSlept++)
                {
                    Thread.Sleep(1000);
                }

                ConsoleWrite("[Connection] Establishing a connection to a Teamspeak 3 Server.");
                _mTsResponse = _mTsConnection.Open(Ts3ServerIp, Ts3QueryPort);
                if (!PerformResponseHandling(Queries.OpenConnectionEstablish))
                {
                    return;
                }
                ConsoleWrite("[Connection] ^2Established a connection to {0}:{1}.", Ts3ServerIp, Ts3QueryPort);

                ConsoleWrite("[Connection] Attempting to login as a Server Query Client.");
                SendTeamspeakQuery(TeamspeakQuery.BuildLoginQuery(Ts3QueryUsername, Ts3QueryPassword));
                if (!PerformResponseHandling(Queries.OpenConnectionLogin))
                {
                    return;
                }
                ConsoleWrite("[Connection] ^2Logged in as {0}.", Ts3QueryUsername);

                ConsoleWrite("[Connection] Attempting to select the correct virtual server.");
                SendTeamspeakQuery(TeamspeakQuery.BuildUsePortQuery(Ts3ServerPort));
                if (!PerformResponseHandling(Queries.OpenConnectionUse))
                {
                    return;
                }
                ConsoleWrite("[Connection] ^2Selected the virtual server using port {0}.", Ts3ServerPort);

                ConsoleWrite("[Connection] Attempting to find the main channel.");
                SendTeamspeakQuery(TeamspeakQuery.BuildChannelFindQuery(Ts3MainChannelName));
                if (!PerformResponseHandling(Queries.OpenConnectionMain))
                {
                    return;
                }
                _mMainChannel.SetBasicData(_mTsResponse.Sections[0].Groups[0]);
                ConsoleWrite("[Connection] ^2Found the channel named {0}.", _mMainChannel.TsName);

                ConsoleWrite("[Connection] Attempting to alter the Server Query Client's name.");
                SendTeamspeakQuery(TeamspeakQuery.BuildChangeNicknameQuery(Ts3QueryNickname));
                if (!PerformResponseHandling(Queries.OpenConnectionNickname))
                {
                    return;
                }
                if (_mTsResponse.Id != "513")
                {
                    ConsoleWrite("[Connection] ^2Changed the Server Query Client's name to {0}.", Ts3QueryNickname);
                }
                _mTsResponse = new TeamspeakResponse("error id=0 msg=ok");

                ConsoleWrite("[Connection] Attempting to find existing pickup, team, and squad channels.");
                SendTeamspeakQuery(TeamspeakQuery.BuildChannelListQuery());
                List<TeamspeakChannel> tsChannels = new List<TeamspeakChannel>();
                foreach (TeamspeakResponseSection tsResponseSection in _mTsResponse.Sections)
                {
                    foreach (TeamspeakResponseGroup tsResponseGroup in tsResponseSection.Groups)
                    {
                        tsChannels.Add(new TeamspeakChannel(tsResponseGroup));
                    }
                }
                foreach (TeamspeakChannel tsChannel in tsChannels)
                {
                    foreach (String tsName in Ts3SubChannelNames)
                    {
                        if (tsChannel.TsName == tsName)
                        {
                            _mPickupChannels.Add(tsChannel);
                            ConsoleWrite("[Connection] ^2Found ^bPickup^n Channel: {0} ({1}).", tsChannel.TsName, tsChannel.TsId);
                            break;
                        }
                    }
                }

                ConsoleWrite("[Connection] TSCV started.");
                _mEnabled = true;
            }

            private void PerformCloseConnection()
            {
                ConsoleWrite("[Closing] Shutting down TSCV.");

                _mTsConnection.Close();

                ConsoleWrite("[Closing] Cleaning up resources.");
                _mClientTsInfo.Clear();
                _mPickupChannels.Clear();
                _mTsResponse = new TeamspeakResponse("error id=0 msg=ok");
                _mCurrentAction = null;

                ConsoleWrite("[Closing] TSCV stopped.");
                _mEnabled = false;
            }

            private Boolean PerformResponseHandling(Queries queryCode)
            {
                if (_mTsResponse.Id == "0")
                {
                    return true;
                }

                switch (_mTsResponse.Id)
                {
                    case "-1": // Socket was open and we tried to re-establish a connection.
                    case "-5": // Socket was closed and we tried to send a query.
                    case "-6": // The query we tried to send was null.
                        ConsoleWrite("[Error] ^3Minor Error:");
                        ConsoleWrite("[Error] ^3{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        return true;

                    case "-2": // Invalid IP Address.
                    case "-3": // Invalid Port.
                    case "-4": // Error occurred when trying to establish a connection.
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during establishing a connection to the Teamspeak 3 Server.");
                        ConsoleWrite("[Error] ^8Make sure your ^b\"Server Ip\"^n and ^b\"Query Port\"^n are correct.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        if (!_mTsReconnecting && PerformReconnect())
                        {
                            return true;
                        }
                        SetPluginState(false);
                        return false;

                    case "-7": // Error occurred during sending the query.
                    case "-8": // Error occurred during receiving the response.
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during sending and receiving data to the Teamspeak 3 Server.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        if (!_mTsReconnecting && PerformReconnect())
                        {
                            break;
                        }
                        SetPluginState(false);
                        return false;

                    case "3329": // You are temp banned from the server for flooding.
                    case "3331": // You are temp banned from the server for 'x' seconds.
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8You were temporarily banned from the Teamspeak 3 Server for flooding.");
                        ConsoleWrite("[Error] ^8Make sure your ^bProcon's Ip^n is in your ^bTeamspeak 3 Server's Whitelist^n.");
                        ConsoleWrite("[Error] ^8{0}: {1} ({2})", _mTsResponse.Id, _mTsResponse.Message, _mTsResponse.ExtraMessage);
                        SetPluginState(false);
                        return false;
                }

                switch (queryCode)
                {
                    case Queries.OpenConnectionEstablish:
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during establishing a connection to the Teamspeak 3 Server.");
                        ConsoleWrite("[Error] ^8Make sure your ^b\"Server Ip\"^n and ^b\"Query Port\"^n are correct.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        SetPluginState(false);
                        return false;
                    case Queries.OpenConnectionLogin:
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during logging into the Teamspeak 3 Server.");
                        ConsoleWrite("[Error] ^8Make sure your ^b\"Query Username\"^n and ^b\"Query Password\"^n are correct.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        SetPluginState(false);
                        return false;
                    case Queries.OpenConnectionUse:
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during finding the virtual server.");
                        ConsoleWrite("[Error] ^8Make sure your ^b\"Server Port\"^n is correct.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        SetPluginState(false);
                        return false;
                    case Queries.OpenConnectionMain:
                        ConsoleWrite("[Error] ^8Fatal Error:");
                        ConsoleWrite("[Error] ^8An error occurred during finding the main channel.");
                        ConsoleWrite("[Error] ^8Make sure your ^b\"Main Channel Name\"^n is correct and that the channel exists in the Teamspeak 3 Server.");
                        ConsoleWrite("[Error] ^8{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        SetPluginState(false);
                        return false;
                    case Queries.OpenConnectionNickname:
                        ConsoleWrite("[Error] ^3Minor Error:");
                        ConsoleWrite("[Error] ^3An error occurred during changing the server query nickname.");
                        ConsoleWrite("[Error] ^3Make sure your ^b\"Query Nickname\"^n is not already in use.");
                        ConsoleWrite("[Error] ^3{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        return true;
                    case Queries.TsInfoClientList:
                        ConsoleWrite("[Error] ^3Minor Error - Update Teamspeak Information:");
                        ConsoleWrite("[Error] ^3An error occurred during obtaining the Teamspeak Client List.");
                        ConsoleWrite("[Error] ^3{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        return false;
                    case Queries.TsInfoChannelList:
                        ConsoleWrite("[Error] ^3Minor Error - Update Teamspeak Information:");
                        ConsoleWrite("[Error] ^3An error occurred during obtaining the Teamspeak Channel List.");
                        ConsoleWrite("[Error] ^3{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        return false;
                    case Queries.TsInfoClientInfo:
                        ConsoleWrite("[Error] ^3Minor Error - Update Teamspeak Information:");
                        ConsoleWrite("[Error] ^3An error occurred during obtaining an Advanced Client Information.");
                        ConsoleWrite("[Error] ^3{0}: {1}", _mTsResponse.Id, _mTsResponse.Message);
                        return true;
                }
                return true;
            }

            private Boolean PerformReconnect()
            {
                ConsoleWrite("[Reconnect] Attempting to establish a new connection to the Teamspeak 3 Server.");
                _mTsReconnecting = true;
                for (int attempt = 1; attempt <= ErrReconnectOnErrorAttempts; attempt++)
                {
                    if (attempt != 1)
                    {
                        _mTsReconnEvent.WaitOne(ErrReconnectOnErrorInterval);
                    }

                    _mActionMutex.WaitOne();
                    ActionEvent tAction = (_mActions.Count == 0) ? null : _mActions.Peek();
                    _mActionMutex.ReleaseMutex();

                    if (tAction == null || tAction.Command != Commands.ClientDisabled)
                    {
                        _mTsConnection.Close();
                        PerformOpenConnection();
                        if (_mTsResponse.Id == "0")
                        {
                            _mTsReconnecting = false;
                            return true;
                        }

                        ConsoleWrite("[Reconnect] Failed {0}.", (attempt < ErrReconnectOnErrorAttempts) ? ("attempt " + attempt + " out of " + ErrReconnectOnErrorAttempts) : ("the last attempt."));
                    }
                    else
                    {
                        attempt = ErrReconnectOnErrorAttempts + 1;
                    }
                }
                _mTsReconnecting = false;
                return false;
            }

            private void UpdateTsInfo()
            {
                List<TeamspeakClient> clientInfo = new List<TeamspeakClient>();

                SendTeamspeakQuery(TeamspeakQuery.BuildClientListQuery());
                if (!PerformResponseHandling(Queries.TsInfoClientList))
                {
                    return;
                }

                foreach (TeamspeakResponseSection sec in _mTsResponse.Sections)
                {
                    foreach (TeamspeakResponseGroup grp in sec.Groups)
                    {
                        clientInfo.Add(new TeamspeakClient(grp));
                    }
                }

                List<TeamspeakChannel> channelInfo = new List<TeamspeakChannel>();

                SendTeamspeakQuery(TeamspeakQuery.BuildChannelListQuery());
                if (!PerformResponseHandling(Queries.TsInfoChannelList))
                {
                    return;
                }

                foreach (TeamspeakResponseSection sec in _mTsResponse.Sections)
                {
                    foreach (TeamspeakResponseGroup grp in sec.Groups)
                    {
                        channelInfo.Add(new TeamspeakChannel(grp));
                    }
                }

                for (int i = 0; i < clientInfo.Count; i++)
                {
                    Boolean inChannel = false;

                    if (clientInfo[i].MedChannelId == _mMainChannel.TsId)
                    {
                        inChannel = true;
                    }

                    foreach (TeamspeakChannel pickupChannel in _mPickupChannels)
                    {
                        if (clientInfo[i].MedChannelId == pickupChannel.TsId)
                        {
                            inChannel = true;
                        }
                    }

                    if (!inChannel)
                    {
                        clientInfo.RemoveAt(i--);
                    }
                }

                for (int i = 0; i < clientInfo.Count; i++)
                {
                    int? tsId = clientInfo[i].TsId;
                    if (tsId != null)
                    {
                        SendTeamspeakQuery(TeamspeakQuery.BuildClientInfoQuery(tsId.Value));
                    }

                    if (!PerformResponseHandling(Queries.TsInfoClientInfo))
                    {
                        return;
                    }

                    if (_mTsResponse.Id != "0")
                    {
                        continue;
                    }

                    if (!_mTsResponse.HasSections || !_mTsResponse.Sections[0].HasGroups)
                    {
                        continue;
                    }


                    clientInfo[i].SetAdvancedData(_mTsResponse.Sections[0].Groups[0]);
                }

                for (int i = 0; i < clientInfo.Count; i++)
                {
                    if (clientInfo[i].AdvIpAddress == null)
                    {
                        clientInfo.RemoveAt(i--);
                    }
                }

                _mClientTsInfo = clientInfo;

                //Log.Debug(() => DbgClients, "[Clients] Result of Teamspeak Client Update:");
                //foreach (TeamspeakClient tsClient in _mClientTsInfo)
                //Log.Debug(() => DbgClients, "- TS Client [Ip: {0}, Channel: {1}, Name: {2}]", tsClient.AdvIpAddress, tsClient.MedChannelId, tsClient.TsName);
            }

            private void AddToActionQueue(Commands command, params Object[] arguments)
            {
                _mActionMutex.WaitOne();
                if (command == Commands.ClientEnabled || command == Commands.ClientDisabled)
                {
                    Queue<ActionEvent> tNew = new Queue<ActionEvent>();
                    while (_mActions.Count > 0 && (_mActions.Peek().Command == Commands.ClientEnabled || _mActions.Peek().Command == Commands.ClientDisabled))
                    {
                        tNew.Enqueue(_mActions.Dequeue());
                    }

                    Boolean tRelease = tNew.Count == 0;

                    tNew.Clear();
                    tNew.Enqueue(new ActionEvent(command, arguments));
                    while (_mActions.Count > 0)
                    {
                        tNew.Enqueue(_mActions.Dequeue());
                    }
                    _mActions = tNew;

                    if (tRelease)
                    {
                        _mActionSemaphore.Release();
                    }
                }
                else
                {
                    _mActions.Enqueue(new ActionEvent(command, arguments));
                    _mActionSemaphore.Release();
                }
                _mActionMutex.ReleaseMutex();
            }

            private void SendTeamspeakQuery(TeamspeakQuery query)
            {
                TimeSpan delay = TimeSpan.FromMilliseconds(SynDelayQueriesAmount);
                TimeSpan delta = DateTime.Now - _mTsPrevSendTime;
                if (delta <= delay)
                {
                    Thread.Sleep(delay - delta);
                }
                _mTsResponse = _mTsConnection.Send(query);
                _mTsPrevSendTime = DateTime.Now;
            }
        }

    }
}
