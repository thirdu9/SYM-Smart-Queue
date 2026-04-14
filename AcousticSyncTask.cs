using System.Diagnostics;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using MediaBrowser.Common.Configuration; // Crucial for dynamic paths
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using SymSmartQueue.Data;

namespace SymSmartQueue.Tasks
{
    public class AcousticSyncTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly DatabaseManager _dbManager;
        private readonly ILogger<AcousticSyncTask> _logger;
        private readonly IApplicationPaths _appPaths; // Injected path resolver

        // Added IApplicationPaths to the constructor
        public AcousticSyncTask(ILibraryManager libraryManager, DatabaseManager dbManager, ILogger<AcousticSyncTask> logger, IApplicationPaths appPaths)
        {
            _libraryManager = libraryManager;
            _dbManager = dbManager;
            _logger = logger;
            _appPaths = appPaths; 
        }

        public string Name => "SYM Local Acoustic Analysis";
        public string Description => "Analyzes audio files natively extracting SVM metrics, ID3 genres, and languages.";
        public string Category => "SYM Engine";
        public string Key => "SymLocalAcousticSyncTask";

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromHours(24).Ticks
                }
            };
        }

        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("[SYM Engine] Starting Native Acoustic Analysis...");

            // Dynamically resolve the persistent configurations folder
            var symDir = Path.Combine(_appPaths.PluginConfigurationsPath, "SymSmartQueue");
            string essentiaBinary = Path.Combine(symDir, "essentia_streaming_extractor_music");
            string profileYaml = Path.Combine(symDir, "profile.yaml");

            if (!File.Exists(essentiaBinary) || !File.Exists(profileYaml))
            {
                _logger.LogError("[SYM Engine] Binary or YAML missing at {Path}! Halting.", symDir);
                return;
            }

            var queryResult = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Audio },
                IsVirtualItem = false
            });

            var allTracks = queryResult.Items;
            var total = queryResult.TotalRecordCount;
            int processed = 0;
            int savedCount = 0;

            foreach (var track in allTracks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processed++;
                if (total > 0) progress.Report((double)processed / total * 100);

                if (_dbManager.HasAcousticData(track.Id.ToString())) continue;

                string audioFilePath = track.Path;
                if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath)) continue;

                string tempJsonPath = Path.Combine(Path.GetTempPath(), $"{track.Id}_acoustics.json");

                try
                {
                    _logger.LogInformation("[SYM Engine] Analyzing: {TrackName}", track.Name);

                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = essentiaBinary;
                        process.StartInfo.Arguments = $"\"{audioFilePath}\" \"{tempJsonPath}\" \"{profileYaml}\"";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;

                        process.Start();

                        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                        await process.WaitForExitAsync(cancellationToken);

                        if (!string.IsNullOrWhiteSpace(stderr) && (process.ExitCode != 0 || stderr.Contains("Error") || stderr.Contains("Cannot") || stderr.Contains("Failed")))
                        {
                            _logger.LogWarning("[SYM Engine] Essentia Output: {Error}", stderr);
                        }
                    }

                    if (File.Exists(tempJsonPath))
                    {
                        var jsonString = await File.ReadAllTextAsync(tempJsonPath, cancellationToken);
                        using var doc = JsonDocument.Parse(jsonString);

                        double bpm = 0, danceability = 0, aggressive = 0, happy = 0, party = 0, relaxed = 0, sad = 0, electronic = 0, acoustic = 0, voice = 0;
                        string rhythmClass = "unknown";
                        
                        string genres = track.Genres != null && track.Genres.Any() ? string.Join(",", track.Genres) : "Unknown";
                        string language = "unknown";

                        if (doc.RootElement.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("tags", out var tags))
                        {
                            if (tags.TryGetProperty("language", out var langArray) && langArray.ValueKind == JsonValueKind.Array && langArray.GetArrayLength() > 0)
                                language = langArray[0].GetString()?.ToLower() ?? "unknown";
                            else if (tags.TryGetProperty("lang", out var shortLangArray) && shortLangArray.ValueKind == JsonValueKind.Array && shortLangArray.GetArrayLength() > 0)
                                language = shortLangArray[0].GetString()?.ToLower() ?? "unknown";
                            else if (tags.TryGetProperty("script", out var scriptArray) && scriptArray.ValueKind == JsonValueKind.Array && scriptArray.GetArrayLength() > 0)
                                language = scriptArray[0].GetString()?.ToLower() ?? "unknown";

                            if (genres == "Unknown" && tags.TryGetProperty("genre", out var genreArray) && genreArray.ValueKind == JsonValueKind.Array && genreArray.GetArrayLength() > 0)
                            {
                                genres = genreArray[0].GetString() ?? "Unknown";
                            }
                        }

                        if (doc.RootElement.TryGetProperty("rhythm", out var rhythm) && rhythm.TryGetProperty("bpm", out var bpmProp))
                            bpm = bpmProp.GetDouble();

                        if (!doc.RootElement.TryGetProperty("highlevel", out var highlevel))
                        {
                            _logger.LogWarning("[SYM Engine] SILENT FAILURE: 'highlevel' data missing from JSON for {TrackName}", track.Name);
                        }
                        else
                        {
                            if (highlevel.TryGetProperty("danceability", out var moodDance) && moodDance.TryGetProperty("all", out var danceAll) && danceAll.TryGetProperty("danceable", out var danceProp))
                                danceability = danceProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_aggressive", out var moodAgg) && moodAgg.TryGetProperty("all", out var aggAll) && aggAll.TryGetProperty("aggressive", out var aggProp))
                                aggressive = aggProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_happy", out var moodHap) && moodHap.TryGetProperty("all", out var hapAll) && hapAll.TryGetProperty("happy", out var hapProp))
                                happy = hapProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_party", out var moodParty) && moodParty.TryGetProperty("all", out var partyAll) && partyAll.TryGetProperty("party", out var partyProp))
                                party = partyProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_relaxed", out var moodRel) && moodRel.TryGetProperty("all", out var relAll) && relAll.TryGetProperty("relaxed", out var relProp))
                                relaxed = relProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_sad", out var moodSad) && moodSad.TryGetProperty("all", out var sadAll) && sadAll.TryGetProperty("sad", out var sadProp))
                                sad = sadProp.GetDouble();

                            if (highlevel.TryGetProperty("mood_electronic", out var moodElec) && moodElec.TryGetProperty("all", out var elecAll) && elecAll.TryGetProperty("electronic", out var elecProp))
                                electronic = elecProp.GetDouble();
                                
                            if (highlevel.TryGetProperty("mood_acoustic", out var moodAcous) && moodAcous.TryGetProperty("all", out var acousAll) && acousAll.TryGetProperty("acoustic", out var acousProp))
                                acoustic = acousProp.GetDouble();
                                
                            if (highlevel.TryGetProperty("voice_instrumental", out var moodVoice) && moodVoice.TryGetProperty("all", out var voiceAll) && voiceAll.TryGetProperty("voice", out var voiceProp))
                                voice = voiceProp.GetDouble();
                                
                            if (highlevel.TryGetProperty("ismir04_rhythm", out var ismirRhythm) && ismirRhythm.TryGetProperty("value", out var ismirVal))
                                rhythmClass = ismirVal.GetString() ?? "unknown";
                        }

                        _dbManager.SaveAcousticData(track.Id.ToString(), bpm, danceability, aggressive, happy, party, relaxed, sad, electronic, acoustic, voice, rhythmClass, genres, language);
                        savedCount++;
                        
                        _logger.LogInformation("[SYM Engine] Parsed '{Name}' | BPM: {Bpm} | Happy: {Happy} | Party: {Party} | Voice: {Voice} | Lang: {Lang}", 
                            track.Name, Math.Round(bpm), Math.Round(happy, 2), Math.Round(party, 2), Math.Round(voice, 2), language);

                        File.Delete(tempJsonPath); 
                    }
                    else
                    {
                        _logger.LogWarning("[SYM Engine] Native engine failed to generate JSON for: {TrackName}", track.Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SYM Engine] Exception during native analysis of {TrackName}", track.Name);
                    if (File.Exists(tempJsonPath)) File.Delete(tempJsonPath);
                }
            }

            _logger.LogInformation("[SYM Engine] Native analysis complete. Analyzed {SavedCount} new tracks.", savedCount);
        }
    }
}