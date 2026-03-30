using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using PRoCon.Core;
using PRoCon.Core.Players;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        private void MessagingThreadLoop()
        {
            try
            {
                Log.Debug(() => "Starting Messaging Thread", 1);
                Thread.CurrentThread.Name = "Messaging";
                DateTime loopStart = UtcNow();
                while (true)
                {
                    try
                    {
                        Log.Debug(() => "Entering Messaging Thread Loop", 7);
                        if (!_pluginEnabled)
                        {
                            Log.Debug(() => "Detected AdKats not enabled. Exiting thread " + Thread.CurrentThread.Name, 6);
                            break;
                        }

                        //Get all unparsed inbound messages
                        Queue<AChatMessage> inboundMessages;
                        lock (_UnparsedMessageQueue)
                        {
                            if (_UnparsedMessageQueue.Count > 0)
                            {
                                Log.Debug(() => "Inbound messages found. Grabbing.", 6);
                                //Grab all messages in the queue
                                inboundMessages = new Queue<AChatMessage>(_UnparsedMessageQueue.ToArray());
                                //Clear the queue for next run
                                _UnparsedMessageQueue.Clear();
                            }
                            else
                            {
                                inboundMessages = null;
                            }
                        }
                        if (inboundMessages == null)
                        {
                            Log.Debug(() => "No inbound messages. Waiting for Input.", 6);
                            //Wait for input
                            if ((UtcNow() - loopStart).TotalMilliseconds > 1000)
                            {
                                Log.Debug(() => "Warning. " + Thread.CurrentThread.Name + " thread processing completed in " + ((int)((UtcNow() - loopStart).TotalMilliseconds)) + "ms", 4);
                            }
                            _MessageParsingWaitHandle.Reset();
                            _MessageParsingWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                            loopStart = UtcNow();
                            continue;
                        }

                        //Loop through all messages in order that they came in
                        while (inboundMessages.Count > 0)
                        {
                            if (!_pluginEnabled)
                            {
                                break;
                            }
                            Log.Debug(() => "begin reading message", 6);
                            //Dequeue the first/next message
                            AChatMessage messageObject = inboundMessages.Dequeue();

                            Boolean isCommand = false;
                            //Check if the message is a command
                            if (messageObject.Message.StartsWith("@") || messageObject.Message.StartsWith("!") || messageObject.Message.StartsWith("."))
                            {
                                messageObject.Message = messageObject.Message.Substring(1);
                                isCommand = true;
                            }
                            else if (messageObject.Message.StartsWith("/@") || messageObject.Message.StartsWith("/!") || messageObject.Message.StartsWith("/."))
                            {
                                messageObject.Message = messageObject.Message.Substring(2);
                                isCommand = true;
                            }
                            else if (messageObject.Message.StartsWith("/"))
                            {
                                messageObject.Message = messageObject.Message.Substring(1);
                                isCommand = true;
                            }

                            if (isCommand)
                            {
                                //Confirm it's actually a valid command in AdKats
                                String[] splitConfirmCommand = messageObject.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (splitConfirmCommand.Length < 1 ||
                                    !_CommandTextDictionary.ContainsKey(splitConfirmCommand[0].ToLower()))
                                {
                                    Int32 resultVote;
                                    if (_ActivePoll != null &&
                                        splitConfirmCommand.Length > 0 &&
                                        Int32.TryParse(splitConfirmCommand[0].ToLower(), out resultVote))
                                    {
                                        Log.Debug(() => "Poll is active and command is numeric " + resultVote + ", allowing non-standard command.", 4);
                                    }
                                    else
                                    {
                                        isCommand = false;
                                    }
                                }
                            }

                            if (isCommand && _threadsReady && _firstPlayerListComplete)
                            {
                                String[] splitMessage = messageObject.Message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (splitMessage.Length == 3 && splitMessage[0] == "AdKatsInstanceCheck" && _enforceSingleInstance && messageObject.Speaker == "Server")
                                {
                                    //Message is an instance check, confirm it is from this instance
                                    if (splitMessage[1] == _instanceKey)
                                    {
                                        Log.Debug(() => "Instance confirmed. " + splitMessage[2], 7);
                                    }
                                    else
                                    {
                                        //There is another instance of AdKats running on this server, check which is superior
                                        String onlineDurationString = splitMessage[2];
                                        Int32 onlineDurationInt;
                                        if (Int32.TryParse(onlineDurationString, out onlineDurationInt))
                                        {
                                            if (onlineDurationInt > Math.Round((UtcNow() - _AdKatsRunningTime).TotalSeconds))
                                            {
                                                //Other instance has been online longer, disable this instance
                                                OnlineAdminSayMessage("Shutting down this AdKats instance, another instance is already online.");
                                                Log.Warn("Shutting down this AdKats instance, another instance is already online.");
                                                _useKeepAlive = false;
                                                Disable();
                                                return;
                                            }
                                            else
                                            {
                                                OnlineAdminSayMessage("Warning, another running instance of AdKats was detected. That instance will terminate shortly.");
                                            }
                                        }
                                        else
                                        {
                                            Log.Error("Unable to parse plugin instance duration.");
                                        }
                                    }
                                }
                            }

                            if (_PostStatLoggerChatManually)
                            {
                                //Upload the chat message
                                UploadChatLog(messageObject);
                            }

                            //check for player mute case
                            //ignore if it's a server call
                            if (messageObject.Speaker != "Server")
                            {

                                APlayer aPlayer;
                                _PlayerDictionary.TryGetValue(messageObject.Speaker, out aPlayer);
                                if (aPlayer != null)
                                {
                                    if (!_AFKIgnoreChat)
                                    {
                                        //Update player last action
                                        aPlayer.lastAction = UtcNow();
                                    }
                                }
                                else
                                {
                                    // Player not yet in dictionary, skip mute and caps checks
                                    // Command parsing will still proceed below
                                }

                                if (aPlayer != null)
                                {
                                    lock (_RoundMutedPlayers)
                                    {
                                        //Check if the player is muted
                                        Log.Debug(() => "Checking for mute case.", 7);
                                        // Persistent mute?
                                        var persistentMute = GetMatchingVerboseASPlayersOfGroup("persistent_mute", aPlayer).Any();
                                        var persistentForceMute = GetMatchingVerboseASPlayersOfGroup("persistent_mute_force", aPlayer).Any();
                                        // Add persistent mute to RoundMutedPlayers if the player is missing in the list.
                                        if (persistentMute && !_RoundMutedPlayers.ContainsKey(messageObject.Speaker))
                                        {
                                            Log.Debug(() => "Adding missing persistent mute to RoundMutedPlayers.", 4);
                                            _RoundMutedPlayers.Add(messageObject.Speaker, 0);
                                        }
                                        if (persistentForceMute)
                                        {
                                            // Force Mute -> Temp Ban Player
                                            ARecord record = new ARecord();
                                            record.record_time = UtcNow();
                                            record.record_source = ARecord.Sources.Automated;
                                            record.server_id = _serverInfo.ServerID;
                                            record.source_name = "PlayerMuteSystem";
                                            _PlayerDictionary.TryGetValue(messageObject.Speaker, out record.target_player);
                                            record.target_name = messageObject.Speaker;
                                            record.record_message = _PersistentMutedPlayerKickMessage;
                                            record.command_type = GetCommandByKey("player_ban_temp");
                                            record.command_action = GetCommandByKey("player_ban_temp");
                                            record.command_numeric = _ForceMuteBanDuration;
                                            QueueRecordForProcessing(record);
                                            continue;
                                        }
                                        else if (_RoundMutedPlayers.ContainsKey(messageObject.Speaker))
                                        {
                                            // Round, Temp Perma Mute Kill -> Kick
                                            if (_MutedPlayerIgnoreCommands && isCommand)
                                            {
                                                Log.Debug(() => "Player muted, but ignoring since message is command.", 3);
                                            }
                                            else if (messageObject.Hidden)
                                            {
                                                Log.Debug(() => "Player muted, but ignoring since message is hidden.", 3);
                                            }
                                            else
                                            {
                                                Log.Debug(() => "Player is muted and valid. Acting.", 7);
                                                //Increment the muted chat count
                                                _RoundMutedPlayers[messageObject.Speaker] = _RoundMutedPlayers[messageObject.Speaker] + 1;
                                                //Create record
                                                ARecord record = new ARecord();
                                                record.record_time = UtcNow();
                                                record.record_source = ARecord.Sources.Automated;
                                                record.server_id = _serverInfo.ServerID;
                                                record.source_name = "PlayerMuteSystem";
                                                _PlayerDictionary.TryGetValue(messageObject.Speaker, out record.target_player);
                                                record.target_name = messageObject.Speaker;
                                                var chances = persistentMute ? _PersistentMutedPlayerChances : _MutedPlayerChances;
                                                if (_RoundMutedPlayers[messageObject.Speaker] > chances)
                                                {
                                                    record.record_message = persistentMute ? _PersistentMutedPlayerKickMessage : _MutedPlayerKickMessage;
                                                    record.command_type = GetCommandByKey("player_kick");
                                                    record.command_action = GetCommandByKey("player_kick");
                                                }
                                                else
                                                {
                                                    record.record_message = persistentMute ? _PersistentMutedPlayerKillMessage : _MutedPlayerKillMessage;
                                                    record.command_type = GetCommandByKey("player_kill");
                                                    record.command_action = GetCommandByKey("player_kill");
                                                    if (!persistentMute)
                                                        AdminSayMessage(record.GetTargetNames() + " killed for talking while muted. They can speak again next round.");
                                                    else
                                                        AdminSayMessage(record.GetTargetNames() + " killed for talking while being perma/temp muted.");
                                                }
                                                QueueRecordForProcessing(record);
                                                continue;
                                            }
                                        }
                                    }

                                    //Check if the all caps system should act on this player
                                    if (_UseAllCapsLimiter &&
                                        GetStringUpperPercentage(messageObject.Message) >= _AllCapsLimterPercentage &&
                                        messageObject.Message.Length >= _AllCapsLimterMinimumCharacters &&
                                        messageObject.Subset != AChatMessage.ChatSubset.Squad &&
                                        (!_AllCapsLimiterSpecifiedPlayersOnly || GetMatchingVerboseASPlayersOfGroup("blacklist_allcaps", aPlayer).Any()))
                                    {
                                        if (isCommand)
                                        {
                                            Log.Debug(() => aPlayer.GetVerboseName() + " chat triggered all caps, but ignoring since message is command.", 3);
                                        }
                                        else if (messageObject.Hidden)
                                        {
                                            Log.Debug(() => aPlayer.GetVerboseName() + " chat triggered all caps, but ignoring since message is hidden.", 3);
                                        }
                                        else
                                        {
                                            Log.Debug(() => aPlayer.GetVerboseName() + " is speaking in all caps and message is valid. Acting.", 7);
                                            aPlayer.AllCapsMessages++;
                                            if (aPlayer.AllCapsMessages >= _AllCapsLimiterKickThreshold)
                                            {
                                                //Kick
                                                QueueRecordForProcessing(new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_kick"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "ChatManager",
                                                    record_message = "Excessive all-caps in all/team chat.",
                                                    record_time = UtcNow()
                                                });
                                            }
                                            else if (aPlayer.AllCapsMessages >= _AllCapsLimiterKillThreshold)
                                            {
                                                //Kill
                                                QueueRecordForProcessing(new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_kill"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "ChatManager",
                                                    record_message = "All-caps in all/team chat.",
                                                    record_time = UtcNow()
                                                });
                                            }
                                            else if (aPlayer.AllCapsMessages >= _AllCapsLimiterWarnThreshold)
                                            {
                                                //Warn
                                                QueueRecordForProcessing(new ARecord
                                                {
                                                    record_source = ARecord.Sources.Automated,
                                                    server_id = _serverInfo.ServerID,
                                                    command_type = GetCommandByKey("player_warn"),
                                                    command_numeric = 0,
                                                    target_name = aPlayer.player_name,
                                                    target_player = aPlayer,
                                                    source_name = "ChatManager",
                                                    record_message = "All-caps in all/team chat.",
                                                    record_time = UtcNow()
                                                });
                                            }
                                        }
                                    }

                                    //TODO: Maybe add this
                                    if (_pingEnforcerEnable &&
                                        (" " + messageObject.Message.ToLower() + " ").Contains(" ping"))
                                    {
                                        // Send the current ping limit
                                    }
                                } // end if (aPlayer != null)
                            }
                            if (isCommand)
                            {
                                QueueCommandForParsing(messageObject);
                            }
                            else
                            {
                                Log.Debug(() => "Message is regular chat. Ignoring.", 7);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException)
                        {
                            Log.HandleException(new AException("Messaging thread aborted. Exiting."));
                            break;
                        }
                        Log.HandleException(new AException("Error occured in Messaging thread. Skipping current loop.", e));
                    }
                }
                Log.Debug(() => "Ending Messaging Thread", 1);
                Threading.StopWatchdog();
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error occured in messaging thread.", e));
            }
        }

        private void QueuePlayerForForceMove(CPlayerInfo player)
        {
            Log.Debug(() => "Entering queuePlayerForForceMove", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to queue " + player.SoldierName + " for TeamSwap ", 6);
                    lock (_TeamswapForceMoveQueue)
                    {
                        _TeamswapForceMoveQueue.Enqueue(player);
                        _TeamswapWaitHandle.Set();
                        Log.Debug(() => player.SoldierName + " queued for TeamSwap", 6);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player for force-move.", e));
            }
            Log.Debug(() => "Exiting queuePlayerForForceMove", 7);
        }

        private void QueuePlayerForMove(CPlayerInfo player)
        {
            Log.Debug(() => "Entering queuePlayerForMove", 7);
            try
            {
                if (_pluginEnabled)
                {
                    Log.Debug(() => "Preparing to add " + player.SoldierName + " to 'on-death' move dictionary.", 6);
                    lock (_TeamswapOnDeathCheckingQueue)
                    {
                        if (!_TeamswapOnDeathMoveDic.ContainsKey(player.SoldierName))
                        {
                            _TeamswapOnDeathMoveDic.Add(player.SoldierName, player);
                            _TeamswapWaitHandle.Set();
                            Log.Debug(() => player.SoldierName + " added to 'on-death' move dictionary.", 6);
                        }
                        else
                        {
                            Log.Debug(() => player.SoldierName + " already in 'on-death' move dictionary.", 6);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error while queueing player for move.", e));
            }
            Log.Debug(() => "Exiting queuePlayerForMove", 7);
        }

        //runs through both team swap queues and performs the swapping
    }
}
