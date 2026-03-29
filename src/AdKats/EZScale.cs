using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Flurl;
using Flurl.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PRoCon.Core;
using PRoCon.Core.Plugin;

namespace PRoConEvents
{
    public partial class AdKats
    {
        // =====================================================================
        // EZScale API Settings
        // =====================================================================

        private const String EZScaleBaseUrl = "https://api.ezscale.cloud";

        private Boolean _EZScaleEnabled = false;
        private String _EZScaleApiKey = "";
        private Boolean _EZScaleCheatDetectionEnabled = false;
        private Double _EZScaleCheatDetectionThreshold = 2.5;
        private Boolean _EZScaleRiskMonitoringEnabled = false;
        private Double _EZScaleRiskThreshold = 2.5;

        // Batch scoring interval
        private DateTime _lastEZScaleBatchScore = DateTime.MinValue;
        private readonly TimeSpan _EZScaleBatchInterval = TimeSpan.FromMinutes(5);

        // =====================================================================
        // EZScale REST API Client Methods
        // =====================================================================

        /// <summary>
        /// Fetches player information from EZScale API instead of Battlelog.
        /// Called from BattlelogCommThreadLoop when EZScale is enabled.
        /// </summary>
        private void FetchPlayerEZScaleInfo(APlayer aPlayer)
        {
            if (String.IsNullOrEmpty(_EZScaleApiKey))
            {
                Log.Warn("EZScale API key is not configured. Falling back to Battlelog.");
                FetchPlayerBattlelogInformation(aPlayer);
                return;
            }

            try
            {
                // Search for persona by name
                var searchResult = EZScaleGet("/v1/battlelog/personas/search",
                    new { name = aPlayer.player_name });

                if (searchResult == null || searchResult["data"] == null)
                {
                    Log.Debug(() => "EZScale: No persona found for " + aPlayer.player_name + ". Falling back to Battlelog.", 3);
                    FetchPlayerBattlelogInformation(aPlayer);
                    return;
                }

                // Find matching persona
                JToken persona = null;
                foreach (var p in searchResult["data"])
                {
                    if (String.Equals(p["persona_name"]?.ToString(), aPlayer.player_name, StringComparison.OrdinalIgnoreCase))
                    {
                        persona = p;
                        break;
                    }
                }

                if (persona == null)
                {
                    Log.Debug(() => "EZScale: No exact match for " + aPlayer.player_name + ". Falling back to Battlelog.", 3);
                    FetchPlayerBattlelogInformation(aPlayer);
                    return;
                }

                String personaId = persona["persona_id"]?.ToString();
                if (String.IsNullOrEmpty(personaId) || !System.Text.RegularExpressions.Regex.IsMatch(personaId, @"^\d+$"))
                {
                    if (!String.IsNullOrEmpty(personaId))
                    {
                        Log.Warn("EZScale: Invalid persona ID format: " + personaId);
                    }
                    FetchPlayerBattlelogInformation(aPlayer);
                    return;
                }

                // Fetch overview stats
                var overview = EZScaleGet("/v1/battlelog/personas/" + personaId + "/overview");
                if (overview != null && overview["data"] != null)
                {
                    var data = overview["data"];
                    aPlayer.BL_Rank = data["rank"]?.Value<Int32>() ?? 0;
                    aPlayer.BL_KDR = data["kd_ratio"]?.Value<Double>() ?? 0;
                    aPlayer.BL_KPM = data["kills_per_minute"]?.Value<Double>() ?? 0;
                    aPlayer.BL_SPM = (Int64)(data["score_per_minute"]?.Value<Double>() ?? 0);
                    aPlayer.BL_Time = data["time_played"]?.Value<Double>() ?? 0;
                    aPlayer.BL_Kills = data["kills"]?.Value<Int32>() ?? 0;
                    aPlayer.BL_Deaths = data["deaths"]?.Value<Int32>() ?? 0;

                    Log.Debug(() => "EZScale: Fetched overview for " + aPlayer.player_name +
                        " (Rank " + aPlayer.BL_Rank + ", KD " + aPlayer.BL_KDR.ToString("F2") + ")", 3);
                }

                // Fetch cheat detection score if enabled
                if (_EZScaleCheatDetectionEnabled)
                {
                    FetchEZScaleCheatScore(aPlayer, personaId);
                }

                // Queue stat refresh in background
                try
                {
                    EZScalePost("/v1/battlelog/personas/" + personaId + "/refresh");
                }
                catch
                {
                    // Non-critical, ignore errors on refresh trigger
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching EZScale info for " + aPlayer.player_name + ". Falling back to Battlelog.", e));
                FetchPlayerBattlelogInformation(aPlayer);
            }
        }

        /// <summary>
        /// Fetches cheat detection score for a player from EZScale ML API.
        /// </summary>
        private void FetchEZScaleCheatScore(APlayer aPlayer, String personaId)
        {
            try
            {
                var result = EZScaleGet("/v1/ml/cheat-detection/persona/" + personaId);
                if (result != null && result["data"] != null)
                {
                    var data = result["data"];
                    Double anomalyScore = data["anomaly_score"]?.Value<Double>() ?? 0;
                    Boolean isFlagged = data["is_flagged"]?.Value<Boolean>() ?? false;

                    aPlayer.EZScaleAnomalyScore = anomalyScore;
                    aPlayer.EZScaleIsFlagged = isFlagged;

                    if (isFlagged && anomalyScore >= _EZScaleCheatDetectionThreshold)
                    {
                        Log.Warn("EZScale: " + aPlayer.player_name + " flagged with anomaly score " +
                            anomalyScore.ToString("F2") + " (threshold: " + _EZScaleCheatDetectionThreshold.ToString("F2") + ")");

                        // Queue an automated report for admin review
                        ARecord record = new ARecord
                        {
                            record_source = ARecord.Sources.Automated,
                            server_id = _serverInfo.ServerID,
                            command_type = GetCommandByKey("player_report"),
                            command_numeric = 0,
                            target_name = aPlayer.player_name,
                            target_player = aPlayer,
                            source_name = "EZScale",
                            record_message = "EZScale cheat detection flagged (score: " + anomalyScore.ToString("F2") + ")",
                            record_time = UtcNow()
                        };
                        QueueRecordForProcessing(record);
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching EZScale cheat score for " + aPlayer.player_name, e));
            }
        }

        /// <summary>
        /// Fetches risk score for a player from EZScale ML API.
        /// </summary>
        private void FetchEZScaleRiskScore(APlayer aPlayer, String personaId)
        {
            try
            {
                var result = EZScaleGet("/v1/ml/player-risk/persona/" + personaId);
                if (result != null && result["data"] != null)
                {
                    var data = result["data"];
                    Double riskScore = data["risk_score"]?.Value<Double>() ?? 0;

                    aPlayer.EZScaleRiskScore = riskScore;

                    if (riskScore >= _EZScaleRiskThreshold)
                    {
                        Log.Warn("EZScale: " + aPlayer.player_name + " high risk score " +
                            riskScore.ToString("F2") + " (threshold: " + _EZScaleRiskThreshold.ToString("F2") + ")");
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error fetching EZScale risk score for " + aPlayer.player_name, e));
            }
        }

        /// <summary>
        /// Batch scores all online players for cheat detection.
        /// Called periodically from a monitor thread.
        /// </summary>
        private void RunEZScaleBatchScoring()
        {
            if (!_EZScaleEnabled || !_EZScaleCheatDetectionEnabled || String.IsNullOrEmpty(_EZScaleApiKey))
            {
                return;
            }

            if (NowDuration(_lastEZScaleBatchScore) < _EZScaleBatchInterval)
            {
                return;
            }

            _lastEZScaleBatchScore = UtcNow();

            try
            {
                // Collect persona IDs of all online players
                var personaIds = new List<String>();
                lock (_PlayerDictionary)
                {
                    foreach (var kvp in _PlayerDictionary)
                    {
                        if (kvp.Value.player_personaId > 0)
                        {
                            personaIds.Add(kvp.Value.player_personaId.ToString());
                        }
                    }
                }

                if (personaIds.Count == 0) return;

                Log.Debug(() => "EZScale: Running batch cheat scoring for " + personaIds.Count + " players", 3);

                var result = EZScalePost("/v1/ml/cheat-detection/batch",
                    new { persona_ids = personaIds });

                if (result != null && result["data"] is JArray scores)
                {
                    foreach (JObject score in scores)
                    {
                        String pid = score["persona_id"]?.ToString();
                        Double anomalyScore = score["anomaly_score"]?.Value<Double>() ?? 0;
                        Boolean isFlagged = score["is_flagged"]?.Value<Boolean>() ?? false;

                        // Find player by persona ID
                        lock (_PlayerDictionary)
                        {
                            foreach (var kvp in _PlayerDictionary)
                            {
                                if (kvp.Value.player_personaId.ToString() == pid)
                                {
                                    kvp.Value.EZScaleAnomalyScore = anomalyScore;
                                    kvp.Value.EZScaleIsFlagged = isFlagged;

                                    if (isFlagged && anomalyScore >= _EZScaleCheatDetectionThreshold)
                                    {
                                        Log.Warn("EZScale batch: " + kvp.Value.player_name +
                                            " flagged (score: " + anomalyScore.ToString("F2") + ")");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.HandleException(new AException("Error during EZScale batch scoring.", e));
            }
        }

        // =====================================================================
        // EZScale HTTP Helpers
        // =====================================================================

        private JObject EZScaleGet(String path, Object queryParams = null)
        {
            try
            {
                var request = EZScaleBaseUrl
                    .AppendPathSegment(path)
                    .WithOAuthBearerToken(_EZScaleApiKey);

                if (queryParams != null)
                {
                    request = request.SetQueryParams(queryParams);
                }

                String json = request.GetStringAsync().Result;
                return JObject.Parse(json);
            }
            catch (FlurlHttpException ex)
            {
                Log.Debug(() => "EZScale API error on GET " + path + ": " + ex.StatusCode + " " + ex.Message, 2);
                return null;
            }
        }

        private JObject EZScalePost(String path, Object body = null)
        {
            try
            {
                var request = EZScaleBaseUrl
                    .AppendPathSegment(path)
                    .WithOAuthBearerToken(_EZScaleApiKey);

                String json;
                if (body != null)
                {
                    json = request.PostJsonAsync(body).ReceiveString().Result;
                }
                else
                {
                    json = request.PostAsync().ReceiveString().Result;
                }
                return JObject.Parse(json);
            }
            catch (FlurlHttpException ex)
            {
                Log.Debug(() => "EZScale API error on POST " + path + ": " + ex.StatusCode + " " + ex.Message, 2);
                return null;
            }
        }
    }
}
