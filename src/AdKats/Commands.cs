using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using MySqlConnector;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

namespace PRoConEvents
{
    public partial class AdKats
    {
        private void CommandParsingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Command Parsing Thread", 1);
                Thread.CurrentThread.Name = "Command";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Command Parsing Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Sleep for 10ms
                        Threading.Wait(10);

                        //Get all unparsed inbound messages
                        if (_UnparsedCommandQueue.Count > 0)
                        {
                            Log.Debug(() => "Preparing to lock command queue to retrive new commands", 7);
                            Queue<AChatMessage> unparsedCommands;
                            lock (_UnparsedCommandQueue)
                            {
                                Log.Debug(() => "Inbound commands found. Grabbing.", 6);
                                //Grab all messages in the queue
                                unparsedCommands = new Queue<AChatMessage>(_UnparsedCommandQueue.ToArray());
                                //Clear the queue for next run
                                _UnparsedCommandQueue.Clear();
                            }

                            //Loop through all commands in order that they came in
                            while (unparsedCommands.Count > 0)
                            {
                                if (!_pluginEnabled)
                                {
                                    break;
                                }
                                Log.Debug(() => "begin reading command", 6);
                                //Dequeue the first/next command
                                AChatMessage commandMessage = unparsedCommands.Dequeue();

                                ARecord record;
                                if (commandMessage.Speaker == "Server")
                                {
                                    record = new ARecord
                                    {
                                        record_source = ARecord.Sources.ServerCommand,
                                        record_access = ARecord.AccessMethod.HiddenExternal,
                                        source_name = "ProconAdmin",
                                        record_time = commandMessage.Timestamp
                                    };
                                }
                                else
                                {
                                    record = new ARecord
                                    {
                                        record_source = ARecord.Sources.InGame,
                                        source_name = commandMessage.Speaker,
                                        record_time = commandMessage.Timestamp
                                    };
                                    //Assign access method
                                    if (commandMessage.Hidden)
                                    {
                                        if (commandMessage.Subset == AChatMessage.ChatSubset.Global)
                                        {
                                            record.record_access = ARecord.AccessMethod.HiddenGlobal;
                                        }
                                        else if (commandMessage.Subset == AChatMessage.ChatSubset.Team)
                                        {
                                            record.record_access = ARecord.AccessMethod.HiddenTeam;
                                        }
                                        else if (commandMessage.Subset == AChatMessage.ChatSubset.Squad)
                                        {
                                            record.record_access = ARecord.AccessMethod.HiddenSquad;
                                        }
                                    }
                                    else
                                    {
                                        if (commandMessage.Subset == AChatMessage.ChatSubset.Global)
                                        {
                                            record.record_access = ARecord.AccessMethod.PublicGlobal;
                                        }
                                        else if (commandMessage.Subset == AChatMessage.ChatSubset.Team)
                                        {
                                            record.record_access = ARecord.AccessMethod.PublicTeam;
                                        }
                                        else if (commandMessage.Subset == AChatMessage.ChatSubset.Squad)
                                        {
                                            record.record_access = ARecord.AccessMethod.PublicSquad;
                                        }
                                    }
                                }

                                //Complete the record creation
                                CompleteRecordInformation(record, commandMessage);
                            }
                        }
                        else
                        {
                            Log.Debug(() => "No inbound commands, ready.", 7);
                            //No commands to parse, ready.
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _CommandParsingWaitHandle.Reset();
                            _CommandParsingWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("Command thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Command thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Command Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in command parsing thread.", e));
            }
        }

        //Before calling this, the record is initialized, and command_source/source_name are filled
        public void CompleteRecordInformation(ARecord record, AChatMessage message)
        {
            try
            {
                //Initial split of command by whitespace
                String[] splitMessage = message.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitMessage.Length < 1)
                {
                    Log.Debug(() => "Completely blank command entered", 5);
                    SendMessageToSource(record, "You entered a completely blank command.");
                    FinalizeRecord(record);
                    return;
                }
                String commandString = splitMessage[0].ToLower();
                Log.Debug(() => "Raw " + commandString, 6);
                String remainingMessage = message.Message.TrimStart(splitMessage[0].ToCharArray()).Trim();

                record.server_id = _serverInfo.ServerID;
                record.record_time = UtcNow();

                // Modify the command message if they are voting in a poll
                Int32 resultVote;
                if (_ActivePoll != null &&
                    Int32.TryParse(commandString, out resultVote))
                {
                    // They entered a format consistent with the xVoteMap voting method. !2, /2, etc
                    // Reformat the text so AdKats understands it as the vote command
                    commandString = GetCommandByKey("poll_vote").command_text;
                    // Set the parameter for the vote command to the number they entered
                    remainingMessage = resultVote.ToString();
                }

                //GATE 1: Add Command
                ACommand commandType = null;
                if (_CommandTextDictionary.TryGetValue(commandString, out commandType) && commandType.command_active == ACommand.CommandActive.Active)
                {
                    record.command_type = commandType;
                    record.command_action = commandType;
                    Log.Debug(() => "Command parsed. Command is " + commandType.command_key + ".", 5);
                }
                else
                {
                    //If command not parsable, return without creating
                    Log.Debug(() => "Command not parsable", 6);
                    if (record.record_source == ARecord.Sources.ExternalPlugin)
                    {
                        SendMessageToSource(record, "Command not parsable.");
                    }
                    FinalizeRecord(record);
                    return;
                }

                //GATE 2: Check Access Rights
                if (record.record_source == ARecord.Sources.ServerCommand && !_AllowAdminSayCommands)
                {
                    SendMessageToSource(record, "Access to commands using that method has been disabled in AdKats settings.");
                    FinalizeRecord(record);
                    return;
                }
                if (!_firstPlayerListComplete)
                {
                    if (!_PlayersRequestingCommands.Contains(record.source_name))
                    {
                        _PlayersRequestingCommands.Add(record.source_name);
                    }
                    if (_startupDurations.Count() >= 2)
                    {
                        var averageStartupDuration = TimeSpan.FromSeconds(_startupDurations.Average(span => span.TotalSeconds));
                        var currentStartupDuration = NowDuration(_AdKatsStartTime);
                        if (averageStartupDuration > currentStartupDuration)
                        {
                            //Give estimated remaining time
                            SendMessageToSource(record, "AdKats starting up; ~" +
                                Math.Round(currentStartupDuration.TotalSeconds / averageStartupDuration.TotalSeconds * 100) + "% ready, ~" +
                                FormatTimeString(averageStartupDuration - currentStartupDuration, 3) + " remaining.");
                        }
                        else
                        {
                            //Just say 'shortly'
                            SendMessageToSource(record, "AdKats starting up; Ready shortly.");
                        }
                    }
                    else
                    {
                        if (!_firstUserListComplete)
                        {
                            SendMessageToSource(record, "AdKats starting up; 1/3 ready, " + FormatTimeString(UtcNow() - _AdKatsStartTime, 3) + " elapsed.");
                        }
                        else
                        {
                            SendMessageToSource(record, "AdKats starting up; 2/3 ready, " + FormatTimeString(UtcNow() - _AdKatsStartTime, 3) + " elapsed.");
                        }
                    }
                    FinalizeRecord(record);
                    return;
                }
                _PlayersRequestingCommands.Clear();
                //Check if player has the right to perform what he's asking, only perform for InGame actions
                if (record.record_source == ARecord.Sources.InGame)
                {
                    //Attempt to fetch the source player
                    if (!_PlayerDictionary.TryGetValue(record.source_name, out record.source_player))
                    {
                        Log.Error("Source player not found in server for in-game " + record.command_type.command_key + " command, unable to complete command.");
                        FinalizeRecord(record);
                        return;
                    }
                    if (!HasAccess(record.source_player, record.command_type))
                    {
                        Log.Debug(() => "No rights to call command", 6);
                        //Only tell the user they dont have access if the command is active
                        if (record.command_type.command_active == ACommand.CommandActive.Active)
                        {
                            if (record.command_type.command_playerInteraction &&
                                !PlayerIsAdmin(record.source_player) &&
                                !message.Hidden &&
                                message.Subset != AChatMessage.ChatSubset.Squad)
                            {
                                AdminSayMessage(record.source_player.GetVerboseName() + " is not an admin, they cannot use " + record.command_type.command_name + ".");
                            }
                            var powerLevel = "";
                            if (PlayerIsAdmin(record.source_player))
                            {
                                powerLevel = " (Power Level " + record.source_player.player_role.role_powerLevel + ")";
                            }
                            SendMessageToSource(record, "Your role " + record.source_player.player_role.role_name + powerLevel + " cannot use " + record.command_type.command_name + ".");
                        }
                        FinalizeRecord(record);
                        return;
                    }
                    if (_DiscordPlayerMonitorEnable &&
                        _DiscordPlayerMonitorView &&
                        _DiscordPlayerRequireVoiceForAdmin &&
                        NowDuration(_DiscordManager.LastUpdate).TotalMinutes < 2.5 &&
                        record.command_type.command_playerInteraction &&
                        record.source_player.DiscordObject == null)
                    {
                        SendMessageToSource(record, "Admin commands may only be issued while in discord.");
                        FinalizeRecord(record);
                        return;
                    }
                }

                //GATE 3: Command access method
                if (record.record_source == ARecord.Sources.InGame)
                {
                    switch (record.command_type.command_access)
                    {
                        case ACommand.CommandAccess.AnyHidden:
                            //Require source to be any hidden
                            if (record.record_access != ARecord.AccessMethod.HiddenExternal && record.record_access != ARecord.AccessMethod.HiddenGlobal && record.record_access != ARecord.AccessMethod.HiddenTeam && record.record_access != ARecord.AccessMethod.HiddenSquad)
                            {
                                SendMessageToSource(record, "Use /" + record.command_type.command_text + " to access the " + record.command_type.command_name + " command.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case ACommand.CommandAccess.AnyVisible:
                            //Require source to be any visible
                            if (record.record_access != ARecord.AccessMethod.PublicExternal && record.record_access != ARecord.AccessMethod.PublicGlobal && record.record_access != ARecord.AccessMethod.PublicTeam && record.record_access != ARecord.AccessMethod.PublicSquad)
                            {
                                SendMessageToSource(record, "Use !" + record.command_type.command_text + ", !" + record.command_type.command_text + ", or ." + record.command_type.command_text + " to access the " + record.command_type.command_name + " command.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case ACommand.CommandAccess.GlobalVisible:
                            //Require source to be global visible
                            if (record.record_access != ARecord.AccessMethod.PublicGlobal)
                            {
                                SendMessageToSource(record, "Use !" + record.command_type.command_text + ", !" + record.command_type.command_text + ", or ." + record.command_type.command_text + " in GLOBAL chat to access the " + record.command_type.command_name + " command.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case ACommand.CommandAccess.TeamVisible:
                            //Require source to be global visible
                            if (record.record_access != ARecord.AccessMethod.PublicTeam)
                            {
                                SendMessageToSource(record, "Use !" + record.command_type.command_text + ", !" + record.command_type.command_text + ", or ." + record.command_type.command_text + " in TEAM chat to access the " + record.command_type.command_name + " command.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                        case ACommand.CommandAccess.SquadVisible:
                            //Require source to be global visible
                            if (record.record_access != ARecord.AccessMethod.PublicSquad)
                            {
                                SendMessageToSource(record, "Use !" + record.command_type.command_text + ", !" + record.command_type.command_text + ", or ." + record.command_type.command_text + " in SQUAD chat to access the " + record.command_type.command_name + " command.");
                                FinalizeRecord(record);
                                return;
                            }
                            break;
                    }
                }
                Log.Debug(() => "Access type " + record.record_access + " is allowed for " + record.command_type.command_key + ".", 4);

                //GATE 4: Specific data based on command type.
                switch (record.command_type.command_key)
                {
                    case "player_move":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        break;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_fmove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "self_teamswap":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //May only call this command from in-game
                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                FinalizeRecord(record);
                                return;
                            }
                            record.record_message = "TeamSwap";
                            record.target_name = record.source_name;
                            CompleteTargetInformation(record, false, false, false);
                        }
                        break;
                    case "self_assist":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //May only call this command from in-game
                            if (record.record_source != ARecord.Sources.InGame || record.source_player == null)
                            {
                                SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Cannot call this command when game not active
                            if (_roundState != RoundState.Playing)
                            {
                                SendMessageToSource(record, Log.CViolet("You can't use assist unless a round is active."));
                                FinalizeRecord(record);
                                return;
                            }

                            //Player Info Check
                            record.record_message = "Assist Losing Team";
                            record.target_name = record.source_name;
                            if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                            {
                                SendMessageToSource(record, "Player information not found. Unable to process command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //May only call this command from in-game
                            if (record.source_player.player_type != PlayerType.Player)
                            {
                                SendMessageToSource(record, Log.CViolet("You must be a player to use assist."));
                                FinalizeRecord(record);
                                return;
                            }

                            var assists = _roundAssists.Values;
                            if (assists.Any())
                            {
                                //Timeout or over-queueing
                                Double secondTimeout = 20;
                                Double timeout = (secondTimeout - (UtcNow() - assists.Max(aRecord => aRecord.record_time_update)).TotalSeconds);
                                if (timeout > 0)
                                {
                                    SendMessageToSource(record, Log.CViolet("Assist recently used. Please wait " + Math.Ceiling(timeout) + " seconds before using it. Thank you."));
                                    FinalizeRecord(record);
                                    return;
                                }
                            }

                            if (_AssistAttemptQueue.Any())
                            {
                                lock (_AssistAttemptQueue)
                                {
                                    var aRecord = _AssistAttemptQueue.FirstOrDefault(assistRecord => assistRecord.target_player.player_id == record.target_player.player_id);
                                    if (aRecord != null)
                                    {
                                        // Refresh the creation time since they manually requested it again
                                        aRecord.record_creationTime = UtcNow();
                                        SendMessageToSource(record, Log.CViolet("You are already queued for automatic assist. You will be moved if possible."));
                                        FinalizeRecord(record);
                                        return;
                                    }
                                }
                            }

                            if (RunAssist(record.target_player, record, null, false))
                            {
                                QueueRecordForProcessing(record);
                            }
                        }
                        break;
                    case "player_debugassist":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Debug Assist Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.record_message = "Debug Assist Player";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_kill":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //May only call this command from in-game
                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                FinalizeRecord(record);
                                return;
                            }

                            record.record_message = "Self-Inflicted";
                            record.target_name = record.source_name;
                            CompleteTargetInformation(record, false, false, false);
                        }
                        break;
                    case "player_kill":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_kill_force":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_warn":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Warning Yourself";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_kick":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_ban_temp":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            //Default is minutes
                            Double recordDuration = 0.0;
                            Double durationMultiplier = 1.0;
                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration.EndsWith("s"))
                                {
                                    stringDuration = stringDuration.TrimEnd('s');
                                    durationMultiplier = (1.0 / 60.0);
                                }
                                else if (stringDuration.EndsWith("m"))
                                {
                                    stringDuration = stringDuration.TrimEnd('m');
                                    durationMultiplier = 1.0;
                                }
                                else if (stringDuration.EndsWith("h"))
                                {
                                    stringDuration = stringDuration.TrimEnd('h');
                                    durationMultiplier = 60.0;
                                }
                                else if (stringDuration.EndsWith("d"))
                                {
                                    stringDuration = stringDuration.TrimEnd('d');
                                    durationMultiplier = 1440.0;
                                }
                                else if (stringDuration.EndsWith("w"))
                                {
                                    stringDuration = stringDuration.TrimEnd('w');
                                    durationMultiplier = 10080.0;
                                }
                                else if (stringDuration.EndsWith("y"))
                                {
                                    stringDuration = stringDuration.TrimEnd('y');
                                    durationMultiplier = 525949.0;
                                }
                                if (!Double.TryParse(stringDuration, out recordDuration))
                                {
                                    SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.command_numeric = (int)(recordDuration * durationMultiplier);
                                if (record.command_numeric > 5259490.0)
                                {
                                    SendMessageToSource(record, "You cannot temp ban for longer than 10 years. Issue a permanent ban instead.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                if (record.command_numeric > _MaxTempBanDuration.TotalMinutes)
                                {
                                    SendMessageToSource(record, "You cannot temp ban for longer than " + FormatTimeString(_MaxTempBanDuration, 2) + ". Defaulting to max temp ban time.");
                                    record.command_numeric = (int)_MaxTempBanDuration.TotalMinutes;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    //Target is source
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 2:
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 3:
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    Log.Debug(() => "reason: " + record.record_message, 6);

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
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
                    case "player_ban_perm":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
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
                    case "player_ban_perm_future":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }
                            if (!_UseBanEnforcer || !_UseBanEnforcerPreviousState)
                            {
                                SendMessageToSource(record, " can only be used when ban enforcer is enabled.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            //Default is minutes
                            Double recordDuration = 0.0;
                            Double durationMultiplier = 1.0;
                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration.EndsWith("s"))
                                {
                                    stringDuration = stringDuration.TrimEnd('s');
                                    durationMultiplier = (1.0 / 60.0);
                                }
                                else if (stringDuration.EndsWith("m"))
                                {
                                    stringDuration = stringDuration.TrimEnd('m');
                                    durationMultiplier = 1.0;
                                }
                                else if (stringDuration.EndsWith("h"))
                                {
                                    stringDuration = stringDuration.TrimEnd('h');
                                    durationMultiplier = 60.0;
                                }
                                else if (stringDuration.EndsWith("d"))
                                {
                                    stringDuration = stringDuration.TrimEnd('d');
                                    durationMultiplier = 1440.0;
                                }
                                else if (stringDuration.EndsWith("w"))
                                {
                                    stringDuration = stringDuration.TrimEnd('w');
                                    durationMultiplier = 10080.0;
                                }
                                else if (stringDuration.EndsWith("y"))
                                {
                                    stringDuration = stringDuration.TrimEnd('y');
                                    durationMultiplier = 525949.0;
                                }
                                if (!Double.TryParse(stringDuration, out recordDuration))
                                {
                                    SendMessageToSource(record, "Invalid time given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                }
                                record.command_numeric = (int)(recordDuration * durationMultiplier);
                            }
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    //Target is source
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, false);
                                    break;
                                case 2:
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);

                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 3:
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);

                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    Log.Debug(() => "reason: " + record.record_message, 6);

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
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
                    case "player_unban":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }
                            if (!_UseBanEnforcer || !_UseBanEnforcerPreviousState)
                            {
                                SendMessageToSource(record, "The unban command can only be used when ban enforcer is enabled.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            String partialName;
                            List<ABan> matchingBans;
                            switch (parameters.Length)
                            {
                                case 0:
                                    //Unban the last player you've banned
                                    SendMessageToSource(record, "Unbanning the last person you banned is not implemented yet.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //Unban the target player
                                    partialName = parameters[0];
                                    record.record_message = "Admin Unban";
                                    break;
                                case 2:
                                    //Unban the target player
                                    partialName = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }

                            if (String.IsNullOrEmpty(partialName) || partialName.Length < 3)
                            {
                                SendMessageToSource(record, "Name search must be at least 3 characters.");
                                FinalizeRecord(record);
                                return;
                            }
                            SendMessageToSource(record, "Fetching matching bans. Please wait.");
                            matchingBans = FetchMatchingBans(partialName, 4);
                            if (matchingBans.Count == 0)
                            {
                                SendMessageToSource(record, "No players matching '" + partialName + "' have active bans.");
                                FinalizeRecord(record);
                                return;
                            }
                            if (matchingBans.Count <= 3)
                            {
                                foreach (ABan innerBan in matchingBans)
                                {
                                    SendMessageToSource(record, innerBan.ban_record.GetTargetNames() + " | " + innerBan.ban_record.record_time.ToShortDateString() + " | " + innerBan.ban_record.record_message);
                                }
                                ABan aBan = matchingBans[0];
                                record.target_name = aBan.ban_record.target_player.player_name;
                                record.target_player = aBan.ban_record.target_player;
                                ConfirmActionWithSource(record);
                            }
                            else
                            {
                                SendMessageToSource(record, "Too many banned players match your search, try again.");
                                FinalizeRecord(record);
                            }
                        }
                        break;
                    case "player_whitelistanticheat":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "AntiCheat Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistping":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_pingEnforcerEnable)
                            {
                                SendMessageToSource(record, "Enable Ping Enforcer to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Ping Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistaa":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_EnableAdminAssistants)
                            {
                                SendMessageToSource(record, "Enable Admin Assistants to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Admin Assistant Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistreport":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Report Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistspambot":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_spamBotExcludeWhitelist)
                            {
                                SendMessageToSource(record, "'Exclude Whitelist from Spam' must be enabled to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Spambot Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistspectator":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Spectator Blacklist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistreport":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Report Source Blacklist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistcommand":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Command Target Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistautoassist":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Auto-Assist Blacklist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistpopulator":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_PopulatorMonitor)
                            {
                                SendMessageToSource(record, "'Monitor Populator Players' must be enabled to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Populator Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistteamkill":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_FeedTeamKillTrackerWhitelist)
                            {
                                SendMessageToSource(record, "'Feed TeamKillTracker Whitelist' must be enabled to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "TeamKillTracker Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_punish":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_dbTimingValid)
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed when database timing is mismatched.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
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
                    case "player_forgive":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
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
                    case "player_mute":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                        FinalizeRecord(record);
                                    }
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_unmute":
                    case "player_persistentmute_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Self-Inflicted";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, false, true);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Unmuting Player";
                                    CompleteTargetInformation(record, false, false, true);
                                    break;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    CompleteTargetInformation(record, false, false, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_join":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (record.source_player != null && record.source_player.player_type == PlayerType.Spectator)
                            {
                                SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("player_join") + " as a spectator.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (record.source_player != null && (record.source_player.player_type == PlayerType.CommanderMobile || record.source_player.player_type == PlayerType.CommanderPC))
                            {
                                SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("player_join") + " as a commander.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "You are already in squad with yourself.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Joining Player";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_pull":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (record.source_player != null && record.source_player.player_type == PlayerType.Spectator)
                            {
                                SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("player_pull") + " as a spectator.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (record.source_player != null && (record.source_player.player_type == PlayerType.CommanderMobile || record.source_player.player_type == PlayerType.CommanderPC))
                            {
                                SendMessageToSource(record, "You cannot use " + GetChatCommandByKey("player_pull") + " as a commander.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "foreveralone.jpg (You cannot pull yourself.)");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use this command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    case "player_roundwhitelist":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            SendMessageToSource(record, "This command has been permanently disabled. - ColColonCleaner");
                            FinalizeRecord(record);
                        }
                        break;
                    case "player_report":
                        {
                            //Get the command text for report
                            String command = GetChatCommandByKey("player_report");

                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "Format must be: " + command + " playername reason");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    SendMessageToSource(record, "Format must be: " + command + " playername reason");
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], false);

                                    Log.Debug(() => "reason: " + record.record_message, 6);

                                    //Only 1 character reasons are required for reports and admin calls
                                    if (record.record_message.Length >= 1)
                                    {
                                        CompleteTargetInformation(record, true, false, false);
                                    }
                                    else
                                    {
                                        Log.Debug(() => "reason too short", 6);
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
                    case "player_calladmin":
                        {
                            //Get the command text for call admin
                            String command = GetChatCommandByKey("player_calladmin");

                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "Format must be: " + command + " playername reason");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    SendMessageToSource(record, "Format must be: " + command + " playername reason");
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], false);

                                    Log.Debug(() => "reason: " + record.record_message, 6);
                                    //Only 1 character reasons are required for reports and admin calls
                                    if (record.record_message.Length >= 1)
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    else
                                    {
                                        Log.Debug(() => "reason too short", 6);
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
                    case "player_info":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Fetching Own Info";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Fetching Player Info";
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_perks":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 1:
                                    if (record.source_player != null && !PlayerIsAdmin(record.source_player))
                                    {
                                        SendMessageToSource(record, "You cannot see another player's perks. Admin only.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = parameters[0];
                                    record.record_message = "Fetching Player Perks";
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Fetching Own Perks";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                            }
                        }
                        break;
                    case "poll_trigger":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_roundState != RoundState.Playing)
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            String pollCode;
                            switch (parameters.Length)
                            {
                                case 1:
                                    pollCode = parameters[0].ToLower();
                                    if (!_AvailablePolls.Contains(pollCode))
                                    {
                                        SendMessageToSource(record, pollCode + " is not an available poll. Available Polls: " + String.Join(", ", _AvailablePolls));
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = pollCode;
                                    record.record_message = "";
                                    QueueRecordForProcessing(record);
                                    break;
                                case 2:
                                    pollCode = parameters[0].ToLower();
                                    if (!_AvailablePolls.Contains(pollCode))
                                    {
                                        SendMessageToSource(record, pollCode + " is not an available poll. Available Polls: " + String.Join(", ", _AvailablePolls));
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = pollCode;
                                    record.record_message = parameters[1].ToLower();
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "No poll code provided. Available Polls: " + String.Join(", ", _AvailablePolls));
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "poll_vote":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                // Do not inform here, simply ignore it
                                FinalizeRecord(record);
                                return;
                            }

                            if (_ActivePoll == null)
                            {
                                SendMessageToSource(record, "There is no active poll to vote on.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (_roundState != RoundState.Playing)
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            Int32 voteNumber;
                            switch (parameters.Length)
                            {
                                case 1:
                                    var paramString = parameters[0].ToLower();
                                    if (!Int32.TryParse(paramString, out voteNumber))
                                    {
                                        SendMessageToSource(record, paramString + " is not a number. Vote options are numbers.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    if (!_ActivePoll.AddVote(record.source_player, voteNumber))
                                    {
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = _ActivePoll.ID;
                                    record.record_message = voteNumber.ToString();
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "No vote option provided.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "poll_cancel":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_ActivePoll == null)
                            {
                                SendMessageToSource(record, "There is no active poll to cancel.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (_roundState != RoundState.Playing)
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            _ActivePoll.Canceled = true;
                        }
                        break;
                    case "poll_complete":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_ActivePoll == null)
                            {
                                SendMessageToSource(record, "There is no active poll to complete");
                                FinalizeRecord(record);
                                return;
                            }

                            if (_roundState != RoundState.Playing)
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            _ActivePoll.Completed = true;
                        }
                        break;
                    case "player_chat":
                        {
                            /*
                                 * This command will get chat history for a player. Comes in 4 variations.
                                 * Variable number of seconds between printed lines, based on the number of characters in the message.
                                 * Oldest to newest. Default last 5 lines, max 30. Spam protection enabled.
                                 *
                                 * /pchat - returns your chat history, default length.
                                 * /pchat (#) - returns your chat history, custom length.
                                 * /pchat (playername) - returns player chat history, default length.
                                 * /pchat (#) (playername) - returns player chat history, custom length.
                                 * /pchat self (playername) - returns last conversation between you and player, default length.
                                 * /pchat (#) self (playername) - returns last conversation between you and player, custom length.
                                 * /pchat (playernameA) (playernameB) - returns last conversation between playerA and playerB, default length.
                                 * /pchat (#) (playernameA) (playernameB) - returns last conversation between playerA and playerB, custom length.
                                 */

                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            Int32 numeric;

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);
                            switch (parameters.Length)
                            {
                                case 0:
                                    //One case, assign to self
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Fetching own chat history";
                                    record.target_name = record.source_name;
                                    record.command_numeric = 5;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    //Two cases
                                    if (Int32.TryParse(parameters[0], out numeric) && numeric <= 30)
                                    {
                                        //Case numeric, assign to duration
                                        record.record_message = "Fetching own chat history";
                                        record.target_name = record.source_name;
                                        record.command_numeric = numeric;
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    else
                                    {
                                        //Case player, assign to target name
                                        record.record_message = "Fetching player chat history";
                                        record.target_name = parameters[0];
                                        record.command_numeric = 5;
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                case 2:
                                    //Three cases
                                    if (Int32.TryParse(parameters[0], out numeric) && numeric <= 30)
                                    {
                                        //Case numeric, assign to duration
                                        record.record_message = "Fetching player chat history";
                                        record.target_name = parameters[1];
                                        record.command_numeric = numeric;
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "");
                                        //Two target case, assign both players
                                        if (parameters[0].ToLower() == "self")
                                        {
                                            //Players are self and target
                                            record.record_message = "Fetching own conversation history.";
                                            record.TargetNamesLocal.Add(record.source_name);
                                            record.TargetNamesLocal.Add(parameters[0]);
                                            record.command_numeric = 5;
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
                                            //Players are target 1 and target 2
                                            record.record_message = "Fetching player conversation history.";
                                            record.TargetNamesLocal.Add(parameters[0]);
                                            record.TargetNamesLocal.Add(parameters[1]);
                                            record.command_numeric = 5;
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                    }
                                    break;
                                case 3:
                                    //Two cases
                                    if (Int32.TryParse(parameters[0], out numeric) && numeric <= 30)
                                    {
                                        //Two target case, assign both players
                                        if (parameters[1].ToLower() == "self")
                                        {
                                            //Players are self and target
                                            record.record_message = "Fetching own conversation history.";
                                            record.TargetNamesLocal.Add(record.source_name);
                                            record.TargetNamesLocal.Add(parameters[2]);
                                            record.command_numeric = numeric;
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
                                            //Players are target 1 and target 2
                                            record.record_message = "Fetching player conversation history.";
                                            record.TargetNamesLocal.Add(parameters[1]);
                                            record.TargetNamesLocal.Add(parameters[2]);
                                            record.command_numeric = numeric;
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "Invalid parameters, unable to submit.");
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
                    case "player_find":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Finding Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Finding Player";
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_lock":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "You can't lock yourself...");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Locking Player";
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_unlock":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "You can't unlock yourself...");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Unlocking Player";
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_mark":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Marking Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Marking Player";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_loadout":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Loadout Fetching Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Loadout Fetching Player";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_ping":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Fetching Own Ping";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Fetching Player Ping";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_forceping":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Forcing Own Manual Ping";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Forcing Player Manual Ping";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_loadout_force":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Loadout Forcing Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Loadout Forcing Player";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_loadout_ignore":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Loadout Ignoring Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Loadout Ignoring Player";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_log":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No log message given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, true, true);
                                        }
                                        else
                                        {
                                            SendMessageToSource(record, "Log message too short, unable to submit.");
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
                    case "self_feedback":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 1:
                                    record.record_message = parameters[0];
                                    record.target_name = "Server";
                                    if (record.record_message.Length < 5)
                                    {
                                        SendMessageToSource(record, "Feedback message too short, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "server_afk":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (_AFKAutoKickEnable)
                                    {
                                        SendMessageToSource(record, "AFK players are being managed automatically; Disable to use this command.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Manage AFK Players";
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
                    case "server_nuke":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_roundState != RoundState.Playing && record.source_name != "ProconAdmin")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    String targetTeam = parameters[0].Trim().ToLower();
                                    record.record_message = "Nuke Server";
                                    Log.Debug(() => "target: " + targetTeam, 6);
                                    List<ATeam> validTeams = _teamDictionary.Values.Where(aTeam => aTeam.TeamID == 1 || aTeam.TeamID == 2).ToList();
                                    ATeam matchingTeam = validTeams.FirstOrDefault(aTeam => aTeam.TeamKey.ToLower() == targetTeam ||
                                                                                            aTeam.TeamID.ToString() == targetTeam);
                                    if (matchingTeam != null)
                                    {
                                        record.target_name = matchingTeam.TeamName;
                                        record.command_numeric = matchingTeam.TeamID;
                                        record.record_message += " (" + matchingTeam.GetTeamIDKey() + ")";
                                    }
                                    else if (targetTeam == "all")
                                    {
                                        record.target_name = "Everyone";
                                        record.record_message += " (Everyone)";
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "Team " + targetTeam.ToUpper() + " not found. Available: " + String.Join(", ", validTeams.Select(aTeam => aTeam.GetTeamIDKey()).ToArray()));
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
                    case "server_nuke_winning":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_roundState != RoundState.Playing && record.source_name != "ProconAdmin")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be used between rounds.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

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
                                if (team1.GetTicketDifferenceRate() > team2.GetTicketDifferenceRate())
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

                                if (winningTeam == mapUpTeam)
                                {
                                    record.command_action = GetCommandByKey("server_nuke");
                                    record.target_name = winningTeam.TeamName;
                                    record.command_numeric = winningTeam.TeamID;
                                    record.record_message = "Nuke Winning Team (" + winningTeam.GetTeamIDKey() + ")";
                                }
                                else
                                {
                                    SendMessageToSource(record, "Winning team must also be map-dominant to issue this command.");
                                    FinalizeRecord(record);
                                    return;
                                }

                                //Have the admin confirm the action
                                ConfirmActionWithSource(record);
                            }
                            else
                            {
                                SendMessageToSource(record, "Unable to fetch teams for nuke winning team command.");
                                FinalizeRecord(record);
                                return;
                            }
                        }
                        break;
                    case "server_countdown":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 3:
                                    //Max 30 seconds
                                    Int32 countdownSeconds;
                                    if (!Int32.TryParse(parameters[0], out countdownSeconds) || countdownSeconds < 1 || countdownSeconds > 30)
                                    {
                                        SendMessageToSource(record, "Invalid duration, must be 1-30. Unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    String targetSubset = parameters[1].ToLower().Trim();
                                    if (String.IsNullOrEmpty(targetSubset))
                                    {
                                        SendMessageToSource(record, "Invalid target, must be squad, team, or all. Unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "target: " + targetSubset, 6);
                                    List<ATeam> validTeams = _teamDictionary.Values.Where(aTeam => aTeam.TeamID == 1 || aTeam.TeamID == 2).ToList();
                                    ATeam matchingTeam = validTeams.FirstOrDefault(aTeam => aTeam.TeamKey.ToLower() == targetSubset.ToLower());
                                    if (matchingTeam != null)
                                    {
                                        record.target_name = matchingTeam.TeamKey;
                                    }
                                    else
                                    {
                                        switch (targetSubset)
                                        {
                                            case "squad":
                                                if (record.source_player == null ||
                                                    !record.source_player.player_online ||
                                                    !_PlayerDictionary.ContainsKey(record.source_player.player_name) ||
                                                    record.source_player.player_type == PlayerType.Spectator)
                                                {
                                                    SendMessageToSource(record, "Must be a player to use squad option. Unable to submit.");
                                                    FinalizeRecord(record);
                                                    return;
                                                }
                                                record.target_name = "Squad";
                                                break;
                                            case "team":
                                                if (record.source_player == null ||
                                                    !record.source_player.player_online ||
                                                    !_PlayerDictionary.ContainsKey(record.source_player.player_name) ||
                                                    record.source_player.player_type == PlayerType.Spectator)
                                                {
                                                    SendMessageToSource(record, "Must be a player to use team option. Unable to submit.");
                                                    FinalizeRecord(record);
                                                    return;
                                                }
                                                record.target_name = "Team";
                                                break;
                                            case "all":
                                                record.target_name = "All";
                                                break;
                                            default:
                                                SendMessageToSource(record, "Invalid target, must be squad, team, or all. Unable to submit.");
                                                FinalizeRecord(record);
                                                return;
                                        }
                                    }
                                    record.command_numeric = countdownSeconds;
                                    String countdownMessage = parameters[2];
                                    if (String.IsNullOrEmpty(countdownMessage))
                                    {
                                        SendMessageToSource(record, "Invalid countdown message, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = countdownMessage;

                                    //Have the admin confirm the action
                                    QueueRecordForProcessing(record);
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

                        if (_serverInfo.ServerType == "OFFICIAL")
                        {
                            SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Non-Admins";
                        record.record_message = "Kick All Players";
                        ConfirmActionWithSource(record);
                        break;
                    case "server_swapnuke":
                        CancelSourcePendingAction(record);

                        if (_serverInfo.ServerType == "OFFICIAL")
                        {
                            SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Everyone";
                        record.record_message = "TeamSwap All Players";
                        ConfirmActionWithSource(record);
                        break;
                    case "round_end":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    String targetTeam = parameters[0];
                                    Log.Debug(() => "target team: " + targetTeam, 6);
                                    record.record_message = "End Round";
                                    List<ATeam> validTeams = _teamDictionary.Values.Where(aTeam => aTeam.TeamID == 1 || aTeam.TeamID == 2).ToList();
                                    ATeam matchingTeam = validTeams.FirstOrDefault(aTeam => aTeam.TeamKey.ToLower() == targetTeam.ToLower());
                                    if (matchingTeam != null)
                                    {
                                        record.target_name = matchingTeam.TeamName;
                                        record.command_numeric = matchingTeam.TeamID;
                                        record.record_message += " (" + matchingTeam.TeamName + ")";
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "Team " + targetTeam.ToUpper() + " not found. Available: " + String.Join(", ", validTeams.Select(aTeam => aTeam.TeamKey).ToArray()));
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

                        if (_serverInfo.ServerType == "OFFICIAL")
                        {
                            SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Restart Round";
                        ConfirmActionWithSource(record);
                        break;
                    case "round_next":
                        CancelSourcePendingAction(record);

                        if (_serverInfo.ServerType == "OFFICIAL")
                        {
                            SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                            FinalizeRecord(record);
                            return;
                        }

                        record.target_name = "Server";
                        record.record_message = "Run Next Map";
                        ConfirmActionWithSource(record);
                        break;
                    case "self_whatis":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], true);
                                    if (record.record_message == null)
                                    {
                                        ACommand aCommand;
                                        if (_CommandTextDictionary.TryGetValue(parameters[0], out aCommand))
                                        {
                                            if (record.source_player == null || HasAccess(record.source_player, aCommand))
                                            {
                                                record.record_message = _CommandDescriptionDictionary[aCommand.command_key];
                                            }
                                            else
                                            {
                                                record.record_message = "Your user role " + record.source_player.player_role.role_name + " does not have access to " + aCommand.command_name + ".";
                                            }
                                        }
                                        else
                                        {
                                            record.record_message = "Invalid PreMessage ID or command name. " + GetChatCommandByKey("self_help") + " for command list. Valid PreMessage IDs are 1-" + _PreMessageList.Count;
                                        }
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
                    case "self_voip":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Send them voip information
                            SendMessageToSource(record, _ServerVoipAddress);
                            FinalizeRecord(record);
                        }
                        break;
                    case "self_challenge":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (EventActive())
                            {
                                SendMessageToSource(record, "Challenges are not enabled during server events.");
                                FinalizeRecord(record);
                                return;
                            }

                            // Target for these commands is always the source.
                            // If there is no source player, then only allow access to info.

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.record_message = "info";
                                    record.target_name = record.source_name;
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    record.record_message = parameters[0];
                                    record.target_name = record.source_name;
                                    QueueRecordForProcessing(record);
                                    break;
                            }
                        }
                        break;
                    case "self_rules":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (EventActive())
                            {
                                SendMessageToSource(record, GetEventDescription(false));
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Requested Rules";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Telling Player Rules";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_surrender":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (EventActive())
                            {
                                SendMessageToSource(record, "Surrender Vote is not available during events.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_surrenderVoteEnable)
                            {
                                SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 0);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Voted for Surrender";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_votenext":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (EventActive())
                            {
                                SendMessageToSource(record, "Surrender Vote is not available during events.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_surrenderVoteEnable)
                            {
                                SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 0);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Voted for Next Round";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_nosurrender":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (EventActive())
                            {
                                SendMessageToSource(record, "Surrender Vote is not available during events.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_surrenderVoteEnable)
                            {
                                SendMessageToSource(record, "Surrender Vote must be enabled in AdKats settings to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 0);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Voted against Surrender";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_help":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Requested Commands";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Telling Player Commands";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_rep":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Requested Reputation";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                case 1:
                                    if (record.source_player != null && !PlayerIsAdmin(record.source_player))
                                    {
                                        SendMessageToSource(record, "You cannot see another player's reputation. Admin only.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = parameters[0];
                                    record.record_message = "Requesting Player Reputation";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_isadmin":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = record.source_name;
                                    record.record_message = "Requesting Admin Status";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                        {
                                            SendMessageToSource(record, "Source player not found, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Requesting Player Admin Status";
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_uptime":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            record.record_message = "Player Requested Uptime";
                            if (record.record_source == ARecord.Sources.InGame)
                            {
                                record.target_name = record.source_name;
                                if (_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                {
                                    record.target_name = record.target_player.player_name;
                                }
                                else
                                {
                                    Log.Error("48204928 this error should never happen.");
                                    FinalizeRecord(record);
                                    return;
                                }
                            }
                            else
                            {
                                record.target_name = "ExternalSource";
                            }
                            QueueRecordForProcessing(record);
                        }
                        break;
                    case "self_contest":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //May only call this command from in-game
                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Player Info Check
                            record.record_message = "Player Contested Report";
                            record.target_name = record.source_name;
                            if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                            {
                                SendMessageToSource(record, "Player information not found. Unable to process command.");
                                FinalizeRecord(record);
                                return;
                            }

                            // Get the latest active report associated with this player
                            ARecord aRecord = null;
                            foreach (ARecord reportRecord in FetchActivePlayerReports())
                            {
                                if (reportRecord.target_player.player_id == record.target_player.player_id)
                                {
                                    if (aRecord == null || reportRecord.record_time > aRecord.record_time)
                                    {
                                        aRecord = reportRecord;
                                    }
                                }
                            }

                            if (aRecord == null)
                            {
                                SendMessageToSource(record, "You have no reports to contest.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Assign the report ID housed in command numeric
                            record.command_numeric = aRecord.command_numeric;
                            //Set Contested
                            aRecord.isContested = true;
                            //Inform All Parties
                            SendMessageToSource(aRecord, aRecord.GetTargetNames() + " has contested your report against them.");
                            SendMessageToSource(record, "You have contested " + aRecord.GetSourceName() + "'s report against you.");
                            OnlineAdminSayMessage(record.GetSourceName() + " has contested report [" + aRecord.command_numeric + "] for " + aRecord.record_message);

                            QueueRecordForProcessing(record);
                        }
                        break;
                    case "self_admins":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            record.record_message = "Player Requested Online Admins";
                            if (record.record_source == ARecord.Sources.InGame)
                            {
                                record.target_name = record.source_name;
                                CompleteTargetInformation(record, false, false, false);
                            }
                            else
                            {
                                record.target_name = "ExternalSource";
                                QueueRecordForProcessing(record);
                            }
                        }
                        break;
                    case "self_lead":
                        {
                            //Remove previous commands awaiting confirmationf
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = "Player Taking Squad Lead";
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Giving Player Squad Lead";
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, false, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_reportlist":
                        {
                            //Remove previous commands awaiting confirmationf
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.record_message = "Listing player reports";
                                    if (record.record_source == ARecord.Sources.InGame)
                                    {
                                        record.target_name = record.source_name;
                                        CompleteTargetInformation(record, false, false, false);
                                    }
                                    else
                                    {
                                        record.target_name = "ExternalSource";
                                        QueueRecordForProcessing(record);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "admin_accept":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "Report ID must be given. Unable to submit.");
                                    FinalizeRecord(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    AcceptPlayerReport(record);
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
                    case "admin_deny":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "Report ID must be given. Unable to submit.");
                                    FinalizeRecord(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    DenyPlayerReport(record);
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
                    case "admin_ignore":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "Report ID must be given. Unable to submit.");
                                    FinalizeRecord(record);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    IgnorePlayerReport(record);
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
                    case "admin_say":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    Log.Debug(() => "" + record.record_message, 6);
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
                    case "player_say":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
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
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    record.record_message = GetPreMessage(parameters[1], false);
                                    Log.Debug(() => "" + record.record_message, 6);

                                    CompleteTargetInformation(record, false, false, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "admin_yell":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    Log.Debug(() => "" + record.record_message, 6);
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
                    case "player_yell":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
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
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    record.record_message = GetPreMessage(parameters[1], false);
                                    Log.Debug(() => "" + record.record_message, 6);

                                    CompleteTargetInformation(record, false, false, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "admin_tell":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    Log.Debug(() => "" + record.record_message, 6);
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
                    case "player_tell":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
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
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    record.record_message = GetPreMessage(parameters[1], false);
                                    Log.Debug(() => "" + record.record_message, 6);

                                    CompleteTargetInformation(record, false, false, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_pm_send":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't start private conversations from outside the game. Use player say.");
                                break;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
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
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    record.record_message = GetPreMessage(parameters[1], false);
                                    Log.Debug(() => "" + record.record_message, 6);

                                    CompleteTargetInformation(record, false, false, false, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_pm_reply":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't reply to private conversations from outside the game. Use player say.");
                                break;
                            }

                            if (record.source_player == null || record.source_player.conversationPartner == null)
                            {
                                SendMessageToSource(record, "You are not in a private conversation. Use /" + GetCommandByKey("player_pm_send").command_text + " player message, to start one.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    record.target_name = record.source_player.conversationPartner.player_name;
                                    record.target_player = record.source_player.conversationPartner;
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "admin_pm_send":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_dequeue":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Dequeueing Self";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "Dequeueing Player";
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistdisperse":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_FeedMultiBalancerDisperseList)
                            {
                                SendMessageToSource(record, "Enable 'Feed MULTIBalancer Even Dispersion List' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Autobalancer Dispersion";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistbalance":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedMultiBalancerWhitelist)
                            {
                                SendMessageToSource(record, "Enable 'Feed MULTIBalancer Whitelist' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Autobalancer Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistbf4db":
                    case "player_whitelistba":
                        {
                            // Rant: GOD THIS IS SO REDUNDANT - .... same code in each case here...
                            // MY EYES ARE BLEEDING :) Hedius.
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            bool isBF4DB = record.command_type.command_key == "player_whitelistbf4db";

                            if ((isBF4DB && !_FeedBF4DBWhitelist) || (!isBF4DB && !_FeedBAWhitelist))
                            {
                                SendMessageToSource(record,
                                isBF4DB ? "Enable 'Feed BF4DB Whitelist' to use this command." : "Enable 'Feed BattlefieldAgency Whitelist' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = isBF4DB ? "BF4DB Whitelist" : "BattlefieldAgency Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_persistentmute":
                    case "player_persistentmute_force":
                        {
                            // Rant: GOD THIS IS SO REDUNDANT - .... same code in each case here...
                            // MY EYES ARE BLEEDING :) Hedius.
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Perma/Temp Muting Player";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    SendMessageToSource(record, "No reason given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);

                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length < _RequiredReasonLength)
                                        {
                                            SendMessageToSource(record, "Reason too short, unable to submit.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_slotreserved":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedServerReservedSlots && !_FeedServerReservedSlots_VSM)
                            {
                                SendMessageToSource(record, "Enable 'Feed Server Reserved Slots' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Reserved Slot";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_slotspectator":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedServerSpectatorList)
                            {
                                SendMessageToSource(record, "Enable 'Feed Server Spectator Slots' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "Spectator Slot";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistreport_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Report Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Report Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistspambot_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_spamBotExcludeWhitelist)
                            {
                                SendMessageToSource(record, "'Exclude Whitelist from Spam' must be enabled to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing SpamBot Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing SpamBot Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistspectator_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Spectator Blacklist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Spectator Blacklist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistreport_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Report Source Blacklist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Report Source Blacklist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistcommand_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Command Target Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Report Target Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistautoassist_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Auto-Assist Blacklist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Auto-Assist Blacklist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistaa_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_EnableAdminAssistants)
                            {
                                SendMessageToSource(record, "Enable Admin Assistants to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Admin Assistant Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Admin Assistant Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistping_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_pingEnforcerEnable)
                            {
                                SendMessageToSource(record, "Enable Ping Enforcer to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Ping Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Ping Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistanticheat_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing AntiCheat Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing AntiCheat Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_slotspectator_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedServerSpectatorList)
                            {
                                SendMessageToSource(record, "Enable 'Feed Server Spectator Slots' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Spectator Slot";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Spectator Slot";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_slotreserved_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedServerReservedSlots)
                            {
                                SendMessageToSource(record, "Enable 'Feed Server Reserved Slots' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Reserved Slot";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Reserved Slot";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistbalance_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedMultiBalancerWhitelist)
                            {
                                SendMessageToSource(record, "Enable 'Feed MULTIBalancer Whitelist' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Autobalance Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Autobalance Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistdisperse_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedMultiBalancerDisperseList)
                            {
                                SendMessageToSource(record, "Enable 'Feed MULTIBalancer Even Dispersion List' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Autobalance Dispersion";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Autobalance Dispersion";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistbf4db_remove":
                    case "player_whitelistba_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            // A dirty try to no copy the code again...
                            bool isBF4DB = record.command_type.command_key == "player_whitelistbf4db";

                            if ((isBF4DB && !_FeedBF4DBWhitelist) || (!isBF4DB && !_FeedBAWhitelist))
                            {
                                SendMessageToSource(record,
                                isBF4DB ? "Enable 'Feed BF4DB Whitelist' to use this command." : "Enable 'Feed BattlefieldAgency Whitelist' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            String defaultMessage = isBF4DB ? "Removing BF4DB Whitelist" : "Removing BattlefieldAgency Whitelist";
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = defaultMessage;
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = defaultMessage;
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistpopulator_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_PopulatorMonitor)
                            {
                                SendMessageToSource(record, "'Monitor Populator Players' must be enabled to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Populator Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Populator Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistteamkill_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_FeedTeamKillTrackerWhitelist)
                            {
                                SendMessageToSource(record, "Enable 'Feed TeamKillTracker Whitelist' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing TeamKillTracker Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing TeamKillTracker Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_watchlist":
                        {
                            // Rant: GOD THIS IS SO REDUNDANT - .... same code in each case here...
                            // MY EYES ARE BLEEDING :) Hedius.
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Adding Player to Watchlist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_watchlist_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Player from Watchlist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Player from Watchlist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistmoveprotection":
                        // Hedius: again redundancy... you know how about we refactor this switch case
                        // coming straight out of hell? Guess I like pain and will keep using it.
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            // This is the only difference between 90 % of all whitelist handlers
                            // this is so awful. Well. Too lazy to refactor and debug this...
                            String defaultReason = "Move Protection Whitelist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_whitelistmoveprotection_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Player from Move Protection Whitelist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Player from Move Protection Whitelist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "plugin_restart":
                        {
                            //Remove previous commands awaiting confirmationf
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = "AdKats";
                                    record.record_message = "Restart AdKats";
                                    ConfirmActionWithSource(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "plugin_update":
                        {
                            //Remove previous commands awaiting confirmationf
                            CancelSourcePendingAction(record);

                            record.target_name = "AdKats";
                            record.record_message = "Update AdKats";
                            QueueRecordForProcessing(record);
                        }
                        break;
                    case "server_shutdown":
                        {
                            //Remove previous commands awaiting confirmationf
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.target_name = "Server";
                                    record.record_message = "Shutdown Server";
                                    ConfirmActionWithSource(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "self_battlecry":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (record.record_source != ARecord.Sources.InGame)
                            {
                                SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    record.record_message = "";
                                    record.target_name = record.source_name;
                                    if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                    {
                                        SendMessageToSource(record, "Source player not found, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                case 1:
                                    record.record_message = GetPreMessage(parameters[0], false);
                                    if (record.record_message.Length > _battlecryMaxLength)
                                    {
                                        SendMessageToSource(record, "Battlecries cannot be longer than " + _battlecryMaxLength + " characters.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    var messageLower = record.record_message.Trim().ToLowerInvariant();
                                    foreach (String deniedWord in _battlecryDeniedWords)
                                    {
                                        if (!String.IsNullOrEmpty(deniedWord.Trim()) && messageLower.Contains(deniedWord.Trim().ToLowerInvariant()))
                                        {
                                            SendMessageToSource(record, "Your battlecry contains denied words. Talk to an admin if this message is in error.");
                                            FinalizeRecord(record);
                                            return;
                                        }
                                    }
                                    record.target_name = record.source_name;
                                    if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                    {
                                        SendMessageToSource(record, "Source player not found, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    QueueRecordForProcessing(record);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_battlecry":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.target_name = parameters[0];
                                    record.record_message = "";
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    record.record_message = GetPreMessage(parameters[1], false);
                                    if (record.record_message.Length > _battlecryMaxLength)
                                    {
                                        SendMessageToSource(record, "Battlecries cannot be longer than " + _battlecryMaxLength + " characters.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_discordlink":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            String tempMemberName = null;
                            DiscordManager.DiscordMember matchingMember = null;
                            switch (parameters.Length)
                            {
                                case 0:
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    record.record_message = "";
                                    record.target_name = record.source_name;
                                    if (!_PlayerDictionary.TryGetValue(record.target_name, out record.target_player))
                                    {
                                        SendMessageToSource(record, "Source player not found, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    tempMemberName = parameters[0];
                                    // Pull the discord member
                                    matchingMember = _DiscordManager.GetMembers(false, true, true)
                                        .FirstOrDefault(aMember => aMember.Name.ToLower().Contains(tempMemberName.ToLower()));
                                    if (matchingMember == null)
                                    {
                                        SendMessageToSource(record, "No matching discord member for '" + tempMemberName + "'.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    if (matchingMember.ID == record.target_player.player_discord_id)
                                    {
                                        SendMessageToSource(record, record.target_player.GetVerboseName() + " already linked with discord member " + matchingMember.Name + ".");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = matchingMember.ID;
                                    QueueRecordForProcessing(record);
                                    break;
                                case 2:
                                    record.target_name = parameters[0];
                                    tempMemberName = parameters[1];
                                    // Pull the discord member
                                    matchingMember = _DiscordManager.GetMembers(false, true, true)
                                        .FirstOrDefault(aMember => aMember.Name.ToLower().Contains(tempMemberName.ToLower()));
                                    if (matchingMember == null)
                                    {
                                        SendMessageToSource(record, "No matching discord member for '" + tempMemberName + "'.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = matchingMember.ID;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Discord link needs a player and a discord member, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistallcaps":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            if (!_UseAllCapsLimiter)
                            {
                                SendMessageToSource(record, "Enable 'Use All Caps Limiter' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            String defaultReason = "All-Caps Chat Blacklist";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_blacklistallcaps_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (!_UseAllCapsLimiter)
                            {
                                SendMessageToSource(record, "Enable 'Use All Caps Limiter' to use this command.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing All-Caps Chat Blacklist";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing All-Caps Chat Blacklist";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "command_confirm":
                        Log.Debug(() => "attempting to confirm command", 6);
                        ARecord recordAttempt = null;
                        _ActionConfirmDic.TryGetValue(record.source_name, out recordAttempt);
                        if (recordAttempt != null)
                        {
                            Log.Debug(() => "command found, calling processing", 6);
                            _ActionConfirmDic.Remove(record.source_name);
                            QueueRecordForProcessing(recordAttempt);
                            FinalizeRecord(record);
                            return;
                        }
                        FinalizeRecord(record);
                        break;
                    case "command_cancel":
                        Log.Debug(() => "attempting to cancel command", 6);
                        if (_ActionConfirmDic.Remove(record.source_name))
                        {
                            SendMessageToSource(record, "Previous command cancelled.");
                        }
                        else if (!_surrenderVoteSucceeded && _surrenderVoteList.Contains(record.source_name))
                        {
                            if (_surrenderVoteList.Remove(record.source_name))
                            {
                                SendMessageToSource(record, "Your vote has been removed!");
                                Int32 requiredVotes = (Int32)((GetPlayerCount() / 2.0) * (_surrenderVoteMinimumPlayerPercentage / 100.0));
                                Int32 voteCount = _surrenderVoteList.Count - _nosurrenderVoteList.Count;
                                OnlineAdminSayMessage(record.GetSourceName() + " removed their surrender vote.");
                                AdminSayMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble. Use " + GetChatCommandByKey("self_surrender") + ", " + GetChatCommandByKey("self_votenext") + ", or " + GetChatCommandByKey("self_nosurrender") + " to vote.");
                                AdminYellMessage((requiredVotes - voteCount) + " votes needed for surrender/scramble");
                            }
                        }
                        FinalizeRecord(record);
                        break;
                    case "player_challenge_play":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Challenge Playing Status";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_challenge_play_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Challenge Playing Status";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Challenge Playing Status";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_challenge_ignore":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Challenge Ignoring Status";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_challenge_ignore_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Removing Challenge Ignoring Status";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Removing Challenge Ignoring Status";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_challenge_autokill":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            String defaultReason = "Challenge Autokill Status";

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 3);

                            if (parameters.Length > 0)
                            {
                                String stringDuration = parameters[0].ToLower();
                                Log.Debug(() => "Raw Duration: " + stringDuration, 6);
                                if (stringDuration == "perm")
                                {
                                    //20 years in minutes
                                    record.command_numeric = 10518984;
                                    defaultReason = "Permanent " + defaultReason;
                                }
                                else
                                {
                                    //Default is minutes
                                    Double recordDuration = 0.0;
                                    Double durationMultiplier = 1.0;
                                    if (stringDuration.EndsWith("s"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('s');
                                        durationMultiplier = (1.0 / 60.0);
                                    }
                                    else if (stringDuration.EndsWith("m"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('m');
                                        durationMultiplier = 1.0;
                                    }
                                    else if (stringDuration.EndsWith("h"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('h');
                                        durationMultiplier = 60.0;
                                    }
                                    else if (stringDuration.EndsWith("d"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('d');
                                        durationMultiplier = 1440.0;
                                    }
                                    else if (stringDuration.EndsWith("w"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('w');
                                        durationMultiplier = 10080.0;
                                    }
                                    else if (stringDuration.EndsWith("y"))
                                    {
                                        stringDuration = stringDuration.TrimEnd('y');
                                        durationMultiplier = 525949.0;
                                    }
                                    if (!Double.TryParse(stringDuration, out recordDuration))
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.command_numeric = (int)(recordDuration * durationMultiplier);
                                    if (record.command_numeric <= 0)
                                    {
                                        SendMessageToSource(record, "Invalid duration given, unable to submit.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    defaultReason = FormatTimeString(TimeSpan.FromMinutes(record.command_numeric), 2) + " " + defaultReason;
                                }
                            }

                            switch (parameters.Length)
                            {
                                case 0:
                                    //No parameters
                                    SendMessageToSource(record, "No parameters given, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                                case 1:
                                    //time
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.target_name = record.source_name;
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 2:
                                    //time
                                    //player
                                    record.target_name = parameters[1];
                                    record.record_message = defaultReason;
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                case 3:
                                    //time
                                    //player
                                    //reason
                                    record.target_name = parameters[1];
                                    Log.Debug(() => "target: " + record.target_name, 6);
                                    record.record_message = GetPreMessage(parameters[2], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    Log.Debug(() => "" + record.record_message, 6);
                                    CompleteTargetInformation(record, false, true, true);
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    case "player_challenge_autokill_remove":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 1);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Challenge AutoKill Status";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, true, true, false);
                                    break;
                                case 1:
                                    record.record_message = "Challenge AutoKill Status";
                                    record.target_name = parameters[0];
                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        CompleteTargetInformation(record, false, true, true);
                                    }
                                    break;
                                default:
                                    SendMessageToSource(record, "Invalid parameters, unable to submit.");
                                    FinalizeRecord(record);
                                    return;
                            }
                        }
                        break;
                    // Hedius: REDUDANCY part 10k. :) This plugin is a mess.
                    case "player_language_punish":
                    case "player_language_reset":
                        {
                            //Remove previous commands awaiting confirmation
                            CancelSourcePendingAction(record);

                            if (_serverInfo.ServerType == "OFFICIAL")
                            {
                                SendMessageToSource(record, record.command_type.command_name + " cannot be performed on official servers.");
                                FinalizeRecord(record);
                                return;
                            }

                            //Parse parameters using max param count
                            String[] parameters = Util.ParseParameters(remainingMessage, 2);
                            switch (parameters.Length)
                            {
                                case 0:
                                    if (record.record_source != ARecord.Sources.InGame)
                                    {
                                        SendMessageToSource(record, "You can't use a self-targeted command from outside the game.");
                                        FinalizeRecord(record);
                                        return;
                                    }
                                    record.record_message = "Issuing command on yourself";
                                    record.target_name = record.source_name;
                                    CompleteTargetInformation(record, false, false, false);
                                    break;
                                case 1:
                                    record.target_name = parameters[0];
                                    //Handle based on report ID as only option
                                    if (!HandlePlayerReport(record))
                                    {
                                        SendMessageToSource(record, "No reason given, unable to submit.");
                                    }
                                    FinalizeRecord(record);
                                    return;
                                case 2:
                                    record.target_name = parameters[0];

                                    //attempt to handle via pre-message ID
                                    record.record_message = GetPreMessage(parameters[1], _RequirePreMessageUse);
                                    if (record.record_message == null)
                                    {
                                        SendMessageToSource(record, "Invalid PreMessage ID, valid PreMessage IDs are 1-" + _PreMessageList.Count);
                                        FinalizeRecord(record);
                                        return;
                                    }

                                    //Handle based on report ID if possible
                                    if (!HandlePlayerReport(record))
                                    {
                                        if (record.record_message.Length >= _RequiredReasonLength)
                                        {
                                            CompleteTargetInformation(record, false, false, true);
                                        }
                                        else
                                        {
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
                    default:
                        Log.Error("Unable to complete record for " + record.command_type.command_key + ", handler not found.");
                        FinalizeRecord(record);
                        return;
                }
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error occured while completing record information.", e));
                FinalizeRecord(record);
            }
        }

        private ATeam GetTeamByKey(String teamKey)
        {
            return _teamDictionary.Values.FirstOrDefault(dTeam => dTeam.TeamKey == teamKey);
        }

        public void FinalizeRecord(ARecord record)
        {
            Log.Debug(() => "Entering FinalizeRecord", 7);
            try
            {
                //Make sure commands are assigned properly
                if (record.command_action == null)
                {
                    if (record.command_type != null)
                    {
                        record.command_action = record.command_type;
                    }
                    else
                    {
                        //Record has no command. Ignore it.
                        return;
                    }
                }
                if (record.external_responseRequested)
                {
                    Hashtable responseHashtable = new Hashtable {
                        {"caller_identity", "AdKats"},
                        {"response_requested", false},
                        {"response_type", "IssueCommand"},
                        {"response_value", CPluginVariable.EncodeStringArray(record.debugMessages.ToArray())}
                    };
                    ExecuteCommand("procon.protected.plugins.call", record.external_responseClass, record.external_responseMethod, "AdKats", JSON.JsonEncode(responseHashtable));
                }
                if (record.record_source == ARecord.Sources.InGame || record.record_source == ARecord.Sources.Automated)
                {
                    Log.Debug(() => "In-Game/Automated " + record.command_action.command_key + " record took " + Math.Round((DateTime.UtcNow - record.record_creationTime).TotalMilliseconds) + "ms to complete actions.", 3);
                }
                //Add event log
                if (String.IsNullOrEmpty(record.target_name))
                {
                    if (record.target_player != null)
                    {
                        record.target_name = record.target_player.player_name;
                    }
                    else
                    {
                        record.target_name = "UnknownTarget";
                    }
                }
                if (String.IsNullOrEmpty(record.source_name))
                {
                    if (record.source_player != null)
                    {
                        record.source_name = record.source_player.player_name;
                    }
                    else
                    {
                        record.source_name = "UnknownSource";
                    }
                }
                String message;
                if (record.record_action_executed)
                {
                    message = record.GetSourceName() + " issued " + record.command_action.command_name + " on " + record.GetTargetNames() + " for " + record.record_message;
                }
                else
                {
                    message = record.GetSourceName() + " FAILED to issue " + record.command_action.command_name + " on " + record.GetTargetNames() + " for " + record.record_message;
                }
                ExecuteCommand("procon.protected.events.write", "Plugins", "PluginAction", message, record.GetSourceName());
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while finalizing record.", e));
            }
            Log.Debug(() => "Exiting FinalizeRecord", 7);
        }

        public void CompleteTargetInformation(ARecord record, Boolean requireConfirm, Boolean externalFetch, Boolean externalOnlineFetch)
        {
            CompleteTargetInformation(record, true, requireConfirm, externalFetch, externalOnlineFetch);
        }

        public void CompleteTargetInformation(ARecord record, Boolean includeLeftPlayers, Boolean requireConfirm, Boolean externalFetch, Boolean externalOnlineFetch)
        {
            try
            {
                Boolean confirmNeeded = false;
                //Multiple target case
                if (record.TargetNamesLocal.Any())
                {
                    foreach (string targetName in record.TargetNamesLocal)
                    {
                        //Attempt to get the player object
                        APlayer aPlayer;
                        String resultMessage;
                        Boolean curConfirm;
                        if (FetchPlayerFromName(targetName, includeLeftPlayers, externalFetch, externalOnlineFetch, out aPlayer, out curConfirm, out resultMessage))
                        {
                            record.TargetPlayersLocal.Add(aPlayer);
                            if (curConfirm)
                            {
                                SendMessageToSource(record, resultMessage);
                                confirmNeeded = true;
                            }
                        }
                        else
                        {
                            SendMessageToSource(record, resultMessage);
                        }
                    }
                    //Ensure main target player is null
                    record.target_player = null;
                }
                //Single target case
                else
                {
                    //Attempt to get the player object
                    APlayer aPlayer;
                    String resultMessage;
                    Boolean curConfirm;
                    if (FetchPlayerFromName(record.target_name, includeLeftPlayers, externalFetch, externalOnlineFetch, out aPlayer, out curConfirm, out resultMessage))
                    {
                        record.target_name = aPlayer.player_name;
                        record.target_player = aPlayer;
                        if (curConfirm)
                        {
                            SendMessageToSource(record, resultMessage);
                            confirmNeeded = true;
                        }
                    }
                    else
                    {
                        SendMessageToSource(record, resultMessage);
                        FinalizeRecord(record);
                        return;
                    }
                }
                if (confirmNeeded)
                {
                    ConfirmActionWithSource(record);
                }
                else
                {
                    QueueRecordForProcessing(record);
                }
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error while completing target information.", e));
                FinalizeRecord(record);
            }
        }

        public Boolean FetchPlayerFromName(String playerNameInput, Boolean includeLeftPlayers, Boolean externalFetch, Boolean externalOnlineFetch, out APlayer aPlayer, out Boolean confirmNeeded, out String resultMessage)
        {
            //Set default return values
            resultMessage = "No valid player found for '" + playerNameInput + "'";
            confirmNeeded = false;
            aPlayer = null;
            try
            {
                if (!IsSoldierNameValid(playerNameInput))
                {
                    resultMessage = "'" + playerNameInput + "' is an invalid player name.";
                    return false;
                }
                //Check for an exact match
                if (_PlayerDictionary.TryGetValue(playerNameInput, out aPlayer))
                {
                    aPlayer.LastUsage = UtcNow();
                    return true;
                }
                if (includeLeftPlayers && _PlayerLeftDictionary.TryGetValue(playerNameInput, out aPlayer))
                {
                    aPlayer.LastUsage = UtcNow();
                    return true;
                }
                //Check online players for substring match
                List<String> currentPlayerNames = _PlayerDictionary.Keys.ToList();
                List<String> leftPlayerNames = _PlayerLeftDictionary.Keys.ToList();
                //Get all subString matches
                List<string> subStringMatches = new List<string>();
                subStringMatches.AddRange(currentPlayerNames.Where(playerName => Regex.Match(playerName, playerNameInput, RegexOptions.IgnoreCase).Success));
                if (subStringMatches.Count == 1)
                {
                    //Only one subString match, call processing without confirmation if able
                    if (_PlayerDictionary.TryGetValue(subStringMatches[0], out aPlayer))
                    {
                        aPlayer.LastUsage = UtcNow();
                        resultMessage = "Player match found for '" + playerNameInput + "'";
                        return true;
                    }
                    Log.Error("Error fetching player for substring match.");
                    resultMessage = "Error fetching player for substring match.";
                    return false;
                }
                if (subStringMatches.Count > 1)
                {
                    //Multiple players matched the query, choose correct one
                    String msg = "'" + playerNameInput + "' matches multiple players: ";
                    bool first = true;
                    String suggestion = null;
                    foreach (String playerName in subStringMatches)
                    {
                        if (first)
                        {
                            msg = msg + playerName;
                            first = false;
                        }
                        else
                        {
                            msg = msg + ", " + playerName;
                        }
                        //Suggest player names that start with the text admins entered over others
                        if (playerName.ToLower().StartsWith(playerNameInput.ToLower()))
                        {
                            suggestion = playerName;
                        }
                    }
                    if (suggestion == null)
                    {
                        //If no player id starts with what admins typed, suggest subString id with lowest Levenshtein distance
                        Int32 bestDistance = Int32.MaxValue;
                        foreach (String playerName in subStringMatches)
                        {
                            Int32 distance = Util.LevenshteinDistance(playerNameInput, playerName);
                            if (distance < bestDistance)
                            {
                                bestDistance = distance;
                                suggestion = playerName;
                            }
                        }
                    }
                    //If the suggestion is still null, something has failed
                    if (suggestion == null)
                    {
                        Log.Error("Name suggestion system failed substring match");
                        resultMessage = "Name suggestion system failed substring match";
                        return false;
                    }

                    //Use suggestion for target
                    if (_PlayerDictionary.TryGetValue(suggestion, out aPlayer))
                    {
                        resultMessage = msg;
                        confirmNeeded = true;
                        aPlayer.LastUsage = UtcNow();
                        return true;
                    }
                    Log.Error("Substring match fetch failed.");
                    resultMessage = "Substring match fetch failed.";
                    return false;
                }
                if (includeLeftPlayers)
                {
                    //There were no players found in the online dictionary. Run a search on the offline dictionary
                    //Get all subString matches
                    List<string> subStringLeftMatches = new List<string>();
                    subStringLeftMatches.AddRange(leftPlayerNames.Where(playerName => Regex.Match(playerName, playerNameInput, RegexOptions.IgnoreCase).Success));
                    if (subStringLeftMatches.Count == 1)
                    {
                        //Only one subString match, call processing without confirmation if able
                        if (_PlayerLeftDictionary.TryGetValue(subStringLeftMatches[0], out aPlayer))
                        {
                            resultMessage = "OFFLINE player match found for '" + playerNameInput + "'";
                            confirmNeeded = true;
                            aPlayer.LastUsage = UtcNow();
                            return true;
                        }
                        Log.Error("Error fetching player for substring match.");
                        resultMessage = "Error fetching player for substring match.";
                        return false;
                    }
                    if (subStringLeftMatches.Count > 1)
                    {
                        //Multiple players matched the query, choose correct one
                        String msg = "'" + playerNameInput + "' matches multiple OFFLINE players: ";
                        bool first = true;
                        String suggestion = null;
                        foreach (String playerName in subStringLeftMatches)
                        {
                            if (first)
                            {
                                msg = msg + playerName;
                                first = false;
                            }
                            else
                            {
                                msg = msg + ", " + playerName;
                            }
                            //Suggest player names that start with the text admins entered over others
                            if (playerName.ToLower().StartsWith(playerNameInput.ToLower()))
                            {
                                suggestion = playerName;
                            }
                        }
                        if (suggestion == null)
                        {
                            //If no player id starts with what admins typed, suggest subString id with lowest Levenshtein distance
                            Int32 bestDistance = Int32.MaxValue;
                            foreach (String playerName in subStringLeftMatches)
                            {
                                Int32 distance = Util.LevenshteinDistance(playerNameInput, playerName);
                                if (distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    suggestion = playerName;
                                }
                            }
                        }
                        //If the suggestion is still null, something has failed
                        if (suggestion == null)
                        {
                            Log.Error("Name suggestion system failed subString match");
                            resultMessage = "Name suggestion system failed subString match";
                            return false;
                        }

                        //Use suggestion for target
                        if (_PlayerLeftDictionary.TryGetValue(suggestion, out aPlayer))
                        {
                            resultMessage = msg;
                            confirmNeeded = true;
                            aPlayer.LastUsage = UtcNow();
                            return true;
                        }
                        Log.Error("Substring match fetch failed.");
                        resultMessage = "Substring match fetch failed.";
                        return false;
                    }
                }
                if (externalFetch)
                {
                    if (playerNameInput.Length < 3)
                    {
                        resultMessage = "No matching online player found, offline search must be at least 3 characters long.";
                        return false;
                    }
                    //No online or left player found, run external fetch over checking for fuzzy match
                    aPlayer = FetchPlayer(false, false, true, null, -1, playerNameInput, null, null, null);
                    if (aPlayer != null)
                    {
                        resultMessage = "Offline player found.";
                        aPlayer.player_online = false;
                        aPlayer.LiveKills.Clear();
                        aPlayer.player_server = null;
                        confirmNeeded = true;
                        aPlayer.LastUsage = UtcNow();
                        return true;
                    }
                }
                if (externalOnlineFetch)
                {
                    //No online or left player found, run external online player fetch over checking for fuzzy match
                    aPlayer = FetchMatchingExternalOnlinePlayer(playerNameInput);
                    if (aPlayer != null)
                    {
                        resultMessage = "Online player found in '" + aPlayer.player_server.ServerName.Substring(0, 20) + "'.";
                        confirmNeeded = true;
                        aPlayer.LastUsage = UtcNow();
                        return true;
                    }
                }
                //No other option, run fuzzy match
                if (currentPlayerNames.Count > 0)
                {
                    //Player not found in either dictionary, run a fuzzy search using Levenshtein Distance on all players in server
                    String fuzzyMatch = null;
                    Int32 bestFuzzyDistance = Int32.MaxValue;
                    foreach (String playerName in currentPlayerNames)
                    {
                        Int32 distance = Util.LevenshteinDistance(playerNameInput, playerName);
                        if (distance < bestFuzzyDistance)
                        {
                            bestFuzzyDistance = distance;
                            fuzzyMatch = playerName;
                        }
                    }
                    //If the suggestion is still null, something has failed
                    if (fuzzyMatch == null)
                    {
                        Log.Error("Name suggestion system failed fuzzy match");
                        resultMessage = "Name suggestion system failed fuzzy match";
                        return false;
                    }
                    if (_PlayerDictionary.TryGetValue(fuzzyMatch, out aPlayer))
                    {
                        resultMessage = "Fuzzy player match found for '" + playerNameInput + "'";
                        confirmNeeded = true;
                        aPlayer.LastUsage = UtcNow();
                        return true;
                    }
                    Log.Error("Player suggestion found matching player, but it could not be fetched.");
                    resultMessage = "Player suggestion found matching player, but it could not be fetched.";
                    return false;
                }
                if (includeLeftPlayers && leftPlayerNames.Count > 0)
                {
                    //No players in the online dictionary, but there are players in the offline dictionary,
                    //run a fuzzy search using Levenshtein Distance on all players who have left
                    String fuzzyMatch = null;
                    Int32 bestFuzzyDistance = Int32.MaxValue;
                    foreach (String playerName in leftPlayerNames)
                    {
                        Int32 distance = Util.LevenshteinDistance(playerNameInput, playerName);
                        if (distance < bestFuzzyDistance)
                        {
                            bestFuzzyDistance = distance;
                            fuzzyMatch = playerName;
                        }
                    }
                    //If the suggestion is still null, something has failed
                    if (fuzzyMatch == null)
                    {
                        Log.Error("Name suggestion system failed fuzzy match");
                        resultMessage = "Name suggestion system failed fuzzy match";
                        return false;
                    }
                    if (_PlayerLeftDictionary.TryGetValue(fuzzyMatch, out aPlayer))
                    {
                        resultMessage = "Fuzzy player match found for '" + playerNameInput + "'";
                        confirmNeeded = true;
                        aPlayer.LastUsage = UtcNow();
                        return true;
                    }
                    Log.Error("Player suggestion found matching player, but it could not be fetched.");
                    resultMessage = "Player suggestion found matching player, but it could not be fetched.";
                    return false;
                }
                Log.Error("Unable to find a matching player.");
                resultMessage = "Unable to find a matching player.";
                return false;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching player from name.", e));
            }
            return false;
        }

        public void ConfirmActionWithSource(ARecord record)
        {
            Log.Debug(() => "Entering confirmActionWithSource", 7);
            try
            {
                if (_bypassCommandConfirmation)
                {
                    QueueRecordForProcessing(record);
                    return;
                }
                lock (_ActionConfirmDic)
                {
                    //Cancel any source pending action
                    CancelSourcePendingAction(record);
                    //Send record to attempt list
                    _ActionConfirmDic.Add(record.source_name, record);

                    SendMessageToSource(record, record.command_type.command_name + "->" + record.GetTargetNames() + " for " + record.record_message + "?");
                }
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error while confirming action with record source.", e));
            }
            Log.Debug(() => "Exiting confirmActionWithSource", 7);
        }

        public void CancelSourcePendingAction(ARecord record)
        {
            Log.Debug(() => "Entering cancelSourcePendingAction", 7);
            try
            {
                Log.Debug(() => "attempting to cancel command", 6);
                lock (_ActionConfirmDic)
                {
                    if (_ActionConfirmDic.Remove(record.source_name))
                    {
                        SendMessageToSource(record, "Previous command Canceled.");
                    }
                }
            }
            catch (Exception e)
            {
                record.record_exception = Log.HandleException(new AException("Error while Cancelling source pending action.", e));
            }
            Log.Debug(() => "Exiting cancelSourcePendingAction", 7);
        }

        public List<ARecord> FetchActivePlayerReports()
        {
            try
            {
                return _PlayerReports.ToList().Where(dRecord => IsActiveReport(dRecord)).ToList();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching active player reports.", e));
            }
            return new List<ARecord>();
        }

        public ARecord FetchPlayerReportByID(String reportIDString)
        {
            ARecord reportedRecord = null;
            try
            {
                Int32 parsedReportID = 0;
                if (Int32.TryParse(reportIDString, out parsedReportID) && parsedReportID > 0)
                {
                    lock (_PlayerReports)
                    {
                        reportedRecord = _PlayerReports.ToList().FirstOrDefault(dRecord => dRecord.command_numeric == parsedReportID);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while fetching player report by report ID.", e));
            }
            return reportedRecord;
        }

        private Int32 AddRecordToReports(ARecord aRecord)
        {
            if (aRecord == null)
            {
                throw new ArgumentNullException("Records cannot be null when adding to reports.");
            }
            try
            {
                Int32 reportID = aRecord.command_numeric;
                if (reportID == 0)
                {
                    var currentReports = _PlayerReports.ToList();
                    if (currentReports.Count() > 500)
                    {
                        // Remove the oldest 50 reports if we are over 500 reports in the log
                        var oldest50Reports = currentReports.OrderBy(record => record.record_id).Take(50);
                        // Can't call RemoveAll with a list -_-
                        foreach (var report in oldest50Reports)
                        {
                            _PlayerReports.Remove(report);
                        }
                    }
                    Random random = new Random();
                    do
                    {
                        reportID = random.Next(100, 999);
                    } while (_PlayerReports.Any(dRecord => dRecord.command_numeric == reportID &&
                                                           IsActiveReport(dRecord)));
                    aRecord.command_numeric = reportID;
                    _PlayerReports.Add(aRecord);
                }
                return reportID;
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while adding record to report list.", e));
            }
            return 0;
        }

        public Boolean DenyPlayerReport(ARecord record)
        {
            try
            {
                Log.Debug(() => "Attempting to handle based on player report.", 6);
                ARecord reportedRecord = FetchPlayerReportByID(record.target_name);
                if (reportedRecord != null)
                {
                    if (!IsActiveReport(reportedRecord))
                    {
                        if (reportedRecord.command_action.command_key == "player_report_ignore")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was ignored, will now be denied.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_confirm")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was accepted, will now be denied.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_deny")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " is already denied.");
                            FinalizeRecord(record);
                            return false;
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_expire")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was expired, will now be denied.");
                        }
                    }
                    Log.Debug(() => "Denying player report.", 5);
                    reportedRecord.command_action = GetCommandByKey("player_report_deny");
                    UpdateRecord(reportedRecord);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been denied.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been denied by " + record.GetSourceName() + ".");
                    record.target_name = reportedRecord.source_name;
                    record.target_player = reportedRecord.source_player;
                    QueueRecordForProcessing(record);
                    return true;
                }
                else
                {
                    SendMessageToSource(record, "Invalid report ID given, unable to submit.");
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while denying player report.", e));
            }
            return false;
        }

        public Boolean IgnorePlayerReport(ARecord record)
        {
            try
            {
                Log.Debug(() => "Attempting to handle based on player report.", 6);
                ARecord reportedRecord = FetchPlayerReportByID(record.target_name);
                if (reportedRecord != null)
                {
                    if (!IsActiveReport(reportedRecord))
                    {
                        if (reportedRecord.command_action.command_key == "player_report_ignore")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " is already ignored.");
                            FinalizeRecord(record);
                            return false;
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_confirm")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was accepted, will now be ignored.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_deny")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was denied, will now be ignored.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_expire")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was expired, will now be ignored.");
                        }
                    }
                    Log.Debug(() => "Ignoring player report.", 5);
                    reportedRecord.command_action = GetCommandByKey("player_report_ignore");
                    UpdateRecord(reportedRecord);
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been ignored by " + record.GetSourceName() + ".");
                    record.target_name = reportedRecord.source_name;
                    record.target_player = reportedRecord.source_player;
                    QueueRecordForProcessing(record);
                    return true;
                }
                else
                {
                    SendMessageToSource(record, "Invalid report ID given, unable to submit.");
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while ignoring player report.", e));
            }
            return false;
        }

        public Boolean AcceptPlayerReport(ARecord record)
        {
            try
            {
                Log.Debug(() => "Attempting to handle based on player report.", 6);
                ARecord reportedRecord = FetchPlayerReportByID(record.target_name);
                if (reportedRecord != null)
                {
                    if (!IsActiveReport(reportedRecord))
                    {
                        if (reportedRecord.command_action.command_key == "player_report_ignore")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was ignored, will now be accepted.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_confirm")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " is already accepted.");
                            FinalizeRecord(record);
                            return false;
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_deny")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was denied, will now be accepted.");
                        }
                        else if (reportedRecord.command_action.command_key == "player_report_expire")
                        {
                            SendMessageToSource(record, "Report " + reportedRecord.command_numeric + " was expired, will now be accepted.");
                        }
                    }
                    Log.Debug(() => "Accepting player report.", 5);
                    ConfirmActiveReport(reportedRecord);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been accepted. Thank you.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been accepted by " + record.GetSourceName() + ".");

                    record.target_name = reportedRecord.source_name;
                    record.target_player = reportedRecord.source_player;

                    record.record_action_executed = true;
                    QueueRecordForProcessing(record);
                    return true;
                }
                else
                {
                    SendMessageToSource(record, "Invalid report ID given, unable to submit.");
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while denying player report.", e));
            }
            return false;
        }

        public Boolean HandlePlayerReport(ARecord record)
        {
            try
            {
                Log.Debug(() => "Attempting to handle based on player report.", 6);
                ARecord reportedRecord = FetchPlayerReportByID(record.target_name);
                if (reportedRecord != null)
                {
                    if (!IsActiveReport(reportedRecord))
                    {
                        SendMessageToSource(record, "Report [" + record.target_name + "] has already been acted on.");
                        return true;
                    }
                    if (record.source_player != null && !PlayerIsAdmin(record.source_player))
                    {
                        return false;
                    }
                    if ((UtcNow() - reportedRecord.record_time).TotalSeconds < _MinimumReportHandleSeconds)
                    {
                        SendMessageToSource(record, "Report [" + record.target_name + "] cannot be acted on. " + FormatTimeString(TimeSpan.FromSeconds(_MinimumReportHandleSeconds - (UtcNow() - reportedRecord.record_time).TotalSeconds), 2) + " remaining.");
                        return true;
                    }
                    if (reportedRecord.isContested)
                    {
                        SendMessageToSource(record, "Report [" + reportedRecord.command_numeric + "] is contested. Please investigate.");
                        if (record.source_player != null)
                        {
                            PlayerYellMessage(record.source_player.player_name, "Report [" + reportedRecord.command_numeric + "] is contested. Please investigate.");
                        }
                        return true;
                    }
                    Log.Debug(() => "Handling player report.", 5);
                    SendMessageToSource(reportedRecord, "Your report [" + reportedRecord.command_numeric + "] has been acted on. Thank you.");
                    OnlineAdminSayMessage("Report [" + reportedRecord.command_numeric + "] has been acted on by " + record.GetSourceName() + ".");
                    ConfirmActiveReport(reportedRecord);

                    record.target_name = reportedRecord.target_name;
                    record.target_player = reportedRecord.target_player;
                    if (String.IsNullOrEmpty(record.record_message) || record.record_message.Length < _RequiredReasonLength)
                    {
                        record.record_message = reportedRecord.record_message;
                    }
                    QueueRecordForProcessing(record);
                    return true;
                }
            }
            catch (Exception e)
            {
                record.record_exception = new AException("Error while handling player report.", e);
                Log.HandleException(record.record_exception);
            }
            return false;
        }

        public Boolean IsActiveReport(ARecord aRecord)
        {
            try
            {
                if (aRecord == null ||
                    aRecord.record_id < 1 ||
                    aRecord.command_type == null ||
                    (aRecord.command_type.command_key != "player_report" && aRecord.command_type.command_key != "player_calladmin") ||
                    aRecord.command_action == null ||
                    aRecord.command_action.command_key == "player_report_confirm" ||
                    aRecord.command_action.command_key == "player_report_ignore" ||
                    aRecord.command_action.command_key == "player_report_deny" ||
                    aRecord.command_action.command_key == "player_report_expire" ||
                    aRecord.target_player == null ||
                    aRecord.TargetSession != aRecord.target_player.ActiveSession)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while checking if a report was active.", e));
            }
            return true;
        }

        public void ConfirmActiveReport(ARecord report)
        {
            try
            {
                if (report != null &&
                    IsActiveReport(report))
                {
                    // Expire all other active reports against the player since this is the one that we acted on
                    var reportsToExpire = report.target_player.TargetedRecords.Where(dRecord => IsActiveReport(dRecord) &&
                                                                                                dRecord.record_id != report.record_id);
                    foreach (var eReport in reportsToExpire)
                    {
                        ExpireActiveReport(eReport);
                    }
                    report.command_action = GetCommandByKey("player_report_confirm");
                    UpdateRecord(report);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while confirming an active report.", e));
            }
        }

        public void ExpireActiveReport(ARecord aRecord)
        {
            try
            {
                if (aRecord != null &&
                    IsActiveReport(aRecord))
                {
                    aRecord.command_action = GetCommandByKey("player_report_expire");
                    UpdateRecord(aRecord);
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while expiring an active report.", e));
            }
        }

        //replaces the message with a pre-message
        public String GetPreMessage(String message, Boolean required)
        {
            Log.Debug(() => "Entering getPreMessage", 7);
            try
            {
                if (!string.IsNullOrEmpty(message))
                {
                    //Attempt to fill the message via pre-message ID
                    Int32 preMessageID = 0;
                    Log.Debug(() => "Raw preMessageID: " + message, 6);
                    Boolean valid = Int32.TryParse(message, out preMessageID);
                    if (valid && (preMessageID > 0) && (preMessageID <= _PreMessageList.Count))
                    {
                        message = _PreMessageList[preMessageID - 1];
                    }
                    else if (required)
                    {
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while getting pre-message.", e));
            }
            Log.Debug(() => "Exiting getPreMessage", 7);
            return message;
        }

        private void QueuePlayerForIPInfoFetch(APlayer aPlayer)
        {
            Log.Debug(() => "Entering QueuePlayerForIPInfoFetch", 6);
            try
            {
                Log.Debug(() => "Preparing to queue player for IP info fetch.", 6);
                lock (_IPInfoFetchQueue)
                {
                    if (_IPInfoFetchQueue.Count() > 70)
                    {
                        //There are more players in the queue than can exist in the server, empty the queue
                        //If players require an info fetch, they will be re-queued by player listing
                        _IPInfoFetchQueue.Clear();
                    }
                    if (//Player is already in the queue, don't re-queue them
                        _IPInfoFetchQueue.Any(qPlayer =>
                        aPlayer.player_id == qPlayer.player_id ||
                        aPlayer.player_guid == qPlayer.player_guid) ||
                        //Player is marked as not online, don't re-queue them
                        !aPlayer.player_online ||
                        //Player is not in the online player dictionary, don't re-queue them
                        !_PlayerDictionary.Values.Any(dPlayer =>
                        aPlayer.player_id == dPlayer.player_id ||
                        aPlayer.player_guid == dPlayer.player_guid))
                    {
                        return;
                    }
                    _IPInfoFetchQueue.Enqueue(aPlayer);
                    Log.Debug(() => "Player queued for IP info fetch.", 6);
                    _IPInfoWaitHandle.Set();
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queuing player for IP info fetch.", e));
            }
            Log.Debug(() => "Exiting QueuePlayerForIPInfoFetch", 6);
        }

    }
}
