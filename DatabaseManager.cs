using Microsoft.Data.Sqlite;
using MediaBrowser.Common.Configuration;

namespace SymSmartQueue.Data
{
    public class TrackData
    {
        public string ItemId { get; set; } = string.Empty;
        public int PlayCount { get; set; }
        public long LastPlayed { get; set; }
    }

    public class AcousticData
    {
        public double Bpm { get; set; }
        public double Danceability { get; set; }
        public double Aggressive { get; set; }
        public double Happy { get; set; }
        public double Party { get; set; }
        public double Relaxed { get; set; }
        public double Sad { get; set; }
        public double Electronic { get; set; }
        public double Acoustic { get; set; }
        public double Voice { get; set; }
        
        // Give the strings default fallback values to satisfy the nullable compiler
        public string Rhythm { get; set; } = "unknown";
        public string Genres { get; set; } = "Unknown";
        public string Language { get; set; } = "unknown";
        public bool IsValid { get; set; }
    }

    public class TasteProfile
    {
        public string TimeBucket { get; set; } = "UNKNOWN";
        public double AvgBpm { get; set; }
        public double AvgDanceability { get; set; }
        public double AvgAggressive { get; set; }
        public double AvgHappy { get; set; }
        public double AvgParty { get; set; }
        public double AvgRelaxed { get; set; }
        public double AvgSad { get; set; }
        public double AvgElectronic { get; set; }
        public double AvgAcoustic { get; set; }
        public double AvgVoice { get; set; }
        public int Interactions { get; set; }
        public double ToleranceMultiplier { get; set; } = 1.0;
    }

    public class DatabaseManager
    {
        private readonly string _dbPath;

        public DatabaseManager(IApplicationPaths appPaths)
        {
            var pluginDir = appPaths.PluginConfigurationsPath;
            if (!Directory.Exists(pluginDir)) Directory.CreateDirectory(pluginDir);
            _dbPath = Path.Combine(pluginDir, "sym_engine.db");
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            // --- 1. TABLE CREATION ---
            var createAcousticTable = @"
                CREATE TABLE IF NOT EXISTS AcousticFeatures (
                    ItemId TEXT PRIMARY KEY,
                    Bpm REAL,
                    Danceability REAL,
                    Aggressive REAL,
                    Happy REAL,
                    Party REAL,
                    Relaxed REAL,
                    Sad REAL,
                    Electronic REAL,
                    Acoustic REAL,
                    Voice REAL,
                    Rhythm TEXT,
                    Genres TEXT,
                    Language TEXT
                );";

            var createTelemetryTable = @"
                CREATE TABLE IF NOT EXISTS UserTelemetry (
                    UserId TEXT,
                    ItemId TEXT,
                    PlayCount INTEGER DEFAULT 0,
                    SkipCount INTEGER DEFAULT 0,
                    LastPlayed INTEGER,
                    PRIMARY KEY (UserId, ItemId)
                );";

            var createTasteProfileTable = @"
                CREATE TABLE IF NOT EXISTS UserTasteProfile (
                    UserId TEXT,
                    TimeBucket TEXT,
                    AvgBpm REAL DEFAULT 0,
                    AvgDanceability REAL DEFAULT 0,
                    AvgAggressive REAL DEFAULT 0,
                    AvgHappy REAL DEFAULT 0,
                    AvgParty REAL DEFAULT 0,
                    AvgRelaxed REAL DEFAULT 0,
                    AvgSad REAL DEFAULT 0,
                    AvgElectronic REAL DEFAULT 0,
                    AvgAcoustic REAL DEFAULT 0,
                    AvgVoice REAL DEFAULT 0,
                    Interactions INTEGER DEFAULT 0,
                    ToleranceMultiplier REAL DEFAULT 1.0,
                    PRIMARY KEY (UserId, TimeBucket)
                );";

            using var cmd1 = new SqliteCommand(createAcousticTable, connection);
            cmd1.ExecuteNonQuery();
            using var cmd2 = new SqliteCommand(createTelemetryTable, connection);
            cmd2.ExecuteNonQuery();
            using var cmd3 = new SqliteCommand(createTasteProfileTable, connection);
            cmd3.ExecuteNonQuery();

            // --- 2. PERFORMANCE INDEXING ---
            var createIndices = @"
                -- Index for the Language filter (High cardinality, heavily queried)
                CREATE INDEX IF NOT EXISTS idx_acoustic_language ON AcousticFeatures(Language);

                -- Composite index for the Standard Queue 'Bounding Box'
                CREATE INDEX IF NOT EXISTS idx_acoustic_smart_queue ON AcousticFeatures(Bpm, Party, Happy, Danceability);

                -- Individual indices for the Heuristic Mood Tabs
                CREATE INDEX IF NOT EXISTS idx_acoustic_relaxed ON AcousticFeatures(Relaxed);
                CREATE INDEX IF NOT EXISTS idx_acoustic_sad ON AcousticFeatures(Sad);
                CREATE INDEX IF NOT EXISTS idx_acoustic_voice ON AcousticFeatures(Voice);
                CREATE INDEX IF NOT EXISTS idx_acoustic_aggressive ON AcousticFeatures(Aggressive);
                
                -- Telemetry composite index to speed up the Time Decay sorting calculations
                CREATE INDEX IF NOT EXISTS idx_telemetry_play_data ON UserTelemetry(UserId, PlayCount, LastPlayed);
            ";

            using var cmd4 = new SqliteCommand(createIndices, connection);
            cmd4.ExecuteNonQuery();
        }
        public void RecordEvent(string userId, string itemId, string action, long timestamp, string timeOfDay)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var isSkip = action == "SKIP" ? 1 : 0;
            var isComplete = action == "COMPLETE" ? 1 : 0;

            var upsertQuery = @"
                INSERT INTO UserTelemetry (UserId, ItemId, PlayCount, SkipCount, LastPlayed)
                VALUES (@UserId, @ItemId, @IsComplete, @IsSkip, @Timestamp)
                ON CONFLICT(UserId, ItemId) DO UPDATE SET
                    PlayCount = PlayCount + @IsComplete,
                    SkipCount = SkipCount + @IsSkip,
                    LastPlayed = CASE WHEN @IsComplete = 1 THEN @Timestamp ELSE LastPlayed END;";

            using var cmd = new SqliteCommand(upsertQuery, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@IsComplete", isComplete);
            cmd.Parameters.AddWithValue("@IsSkip", isSkip);
            cmd.Parameters.AddWithValue("@Timestamp", timestamp);
            cmd.ExecuteNonQuery();

            if (string.IsNullOrEmpty(timeOfDay) || timeOfDay == "UNKNOWN") return;

            var trackAcoustics = GetAcousticsForTrack(itemId);
            if (!trackAcoustics.IsValid) return;

            var profile = GetTasteProfile(userId, timeOfDay);

            if (isComplete == 1)
            {
                profile.AvgBpm = ((profile.AvgBpm * profile.Interactions) + trackAcoustics.Bpm) / (profile.Interactions + 1);
                profile.AvgDanceability = ((profile.AvgDanceability * profile.Interactions) + trackAcoustics.Danceability) / (profile.Interactions + 1);
                profile.AvgAggressive = ((profile.AvgAggressive * profile.Interactions) + trackAcoustics.Aggressive) / (profile.Interactions + 1);
                profile.AvgHappy = ((profile.AvgHappy * profile.Interactions) + trackAcoustics.Happy) / (profile.Interactions + 1);
                profile.AvgParty = ((profile.AvgParty * profile.Interactions) + trackAcoustics.Party) / (profile.Interactions + 1);
                profile.AvgRelaxed = ((profile.AvgRelaxed * profile.Interactions) + trackAcoustics.Relaxed) / (profile.Interactions + 1);
                profile.AvgSad = ((profile.AvgSad * profile.Interactions) + trackAcoustics.Sad) / (profile.Interactions + 1);
                profile.AvgElectronic = ((profile.AvgElectronic * profile.Interactions) + trackAcoustics.Electronic) / (profile.Interactions + 1);
                profile.AvgAcoustic = ((profile.AvgAcoustic * profile.Interactions) + trackAcoustics.Acoustic) / (profile.Interactions + 1);
                profile.AvgVoice = ((profile.AvgVoice * profile.Interactions) + trackAcoustics.Voice) / (profile.Interactions + 1);
                
                profile.Interactions += 1;
                profile.ToleranceMultiplier = Math.Min(1.5, profile.ToleranceMultiplier + 0.05);
            }
            else if (isSkip == 1)
            {
                profile.ToleranceMultiplier = Math.Max(0.5, profile.ToleranceMultiplier - 0.1);
            }

            SaveTasteProfile(userId, profile);
        }

        public List<string> GetSmartQueue(string userId, List<string> seedItemIds, float explorationWeight, int limit, string timeOfDay)
        {
            var acoustics = GetAverageAcoustics(seedItemIds);
            var profile = GetTasteProfile(userId, timeOfDay);

            if (!acoustics.IsValid)
            {
                if (profile.Interactions == 0) return new List<string>(); 
                
                acoustics.Bpm = profile.AvgBpm;
                acoustics.Danceability = profile.AvgDanceability;
                acoustics.Party = profile.AvgParty;
                acoustics.Happy = profile.AvgHappy;
                acoustics.IsValid = true;
            }

            int discoveryLimit = (int)Math.Round(limit * explorationWeight);
            int familiarLimit = limit - discoveryLimit;
            
            var finalQueue = new List<string>();

            if (familiarLimit > 0)
            {
                var familiarCandidates = FetchFamiliarCandidatesFromSql(userId, acoustics, profile.ToleranceMultiplier); 
                
                long currentEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                double decayConstant = 0.0231;

                var scoredTracks = familiarCandidates.Select(track => 
                {
                    double daysSincePlayed = (currentEpoch - track.LastPlayed) / 86400.0;
                    double score = track.PlayCount * Math.Exp(-decayConstant * daysSincePlayed);
                    return new { track.ItemId, Score = score };
                })
                .OrderByDescending(t => t.Score)
                .Take(familiarLimit)
                .Select(t => t.ItemId);

                finalQueue.AddRange(scoredTracks);
            }

            if (discoveryLimit > 0)
            {
                var discoveryCandidates = FetchDiscoveryCandidatesFromSql(userId, acoustics, discoveryLimit, profile.ToleranceMultiplier);
                finalQueue.AddRange(discoveryCandidates);
            }

            if (seedItemIds != null)
            {
                finalQueue.RemoveAll(id => seedItemIds.Contains(id));
            }

            return finalQueue.OrderBy(x => Guid.NewGuid()).ToList();
        }

        private List<TrackData> FetchFamiliarCandidatesFromSql(string userId, AcousticData avg, double tolerance)
        {
            var results = new List<TrackData>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            double bpmRange = 15.0 * tolerance;
            double moodRange = 0.20 * tolerance; 

            var query = @"
                SELECT u.ItemId, u.PlayCount, u.LastPlayed 
                FROM UserTelemetry u
                INNER JOIN AcousticFeatures a ON u.ItemId = a.ItemId
                WHERE u.UserId = @UserId 
                  AND u.PlayCount > 0 
                  AND u.SkipCount < 3
                  AND a.Bpm BETWEEN @MinBpm AND @MaxBpm
                  AND a.Party BETWEEN @MinParty AND @MaxParty
                  AND a.Happy BETWEEN @MinHappy AND @MaxHappy;";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@MinBpm", avg.Bpm - bpmRange);
            cmd.Parameters.AddWithValue("@MaxBpm", avg.Bpm + bpmRange);
            cmd.Parameters.AddWithValue("@MinParty", avg.Party - moodRange);
            cmd.Parameters.AddWithValue("@MaxParty", avg.Party + moodRange);
            cmd.Parameters.AddWithValue("@MinHappy", avg.Happy - moodRange);
            cmd.Parameters.AddWithValue("@MaxHappy", avg.Happy + moodRange);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new TrackData
                {
                    ItemId = reader.GetString(0),
                    PlayCount = reader.GetInt32(1),
                    LastPlayed = reader.GetInt64(2)
                });
            }
            return results;
        }

        private List<string> FetchDiscoveryCandidatesFromSql(string userId, AcousticData avg, int limit, double tolerance)
        {
            var results = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            double bpmRange = 15.0 * tolerance;
            double moodRange = 0.20 * tolerance;

            var query = @"
                SELECT a.ItemId 
                FROM AcousticFeatures a
                LEFT JOIN UserTelemetry u ON a.ItemId = u.ItemId AND u.UserId = @UserId
                WHERE (u.PlayCount IS NULL OR u.PlayCount = 0)
                  AND a.Bpm BETWEEN @MinBpm AND @MaxBpm
                  AND a.Party BETWEEN @MinParty AND @MaxParty
                  AND a.Happy BETWEEN @MinHappy AND @MaxHappy
                ORDER BY RANDOM() 
                LIMIT @Limit;";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.Parameters.AddWithValue("@MinBpm", avg.Bpm - bpmRange);
            cmd.Parameters.AddWithValue("@MaxBpm", avg.Bpm + bpmRange);
            cmd.Parameters.AddWithValue("@MinParty", avg.Party - moodRange);
            cmd.Parameters.AddWithValue("@MaxParty", avg.Party + moodRange);
            cmd.Parameters.AddWithValue("@MinHappy", avg.Happy - moodRange);
            cmd.Parameters.AddWithValue("@MaxHappy", avg.Happy + moodRange);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }
            return results;
        }

        private TasteProfile GetTasteProfile(string userId, string timeBucket)
        {
            var profile = new TasteProfile { TimeBucket = timeBucket };
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var query = @"
                SELECT AvgBpm, AvgDanceability, AvgAggressive, AvgHappy, AvgParty, 
                       AvgRelaxed, AvgSad, AvgElectronic, AvgAcoustic, AvgVoice, Interactions, ToleranceMultiplier 
                FROM UserTasteProfile 
                WHERE UserId = @UserId AND TimeBucket = @TimeBucket;";
                
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TimeBucket", timeBucket);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                profile.AvgBpm = reader.GetDouble(0);
                profile.AvgDanceability = reader.GetDouble(1);
                profile.AvgAggressive = reader.GetDouble(2);
                profile.AvgHappy = reader.GetDouble(3);
                profile.AvgParty = reader.GetDouble(4);
                profile.AvgRelaxed = reader.GetDouble(5);
                profile.AvgSad = reader.GetDouble(6);
                profile.AvgElectronic = reader.GetDouble(7);
                profile.AvgAcoustic = reader.GetDouble(8);
                profile.AvgVoice = reader.GetDouble(9);
                profile.Interactions = reader.GetInt32(10);
                profile.ToleranceMultiplier = reader.GetDouble(11);
            }
            return profile;
        }

        private void SaveTasteProfile(string userId, TasteProfile profile)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var query = @"
                INSERT OR REPLACE INTO UserTasteProfile 
                (UserId, TimeBucket, AvgBpm, AvgDanceability, AvgAggressive, AvgHappy, AvgParty, AvgRelaxed, AvgSad, AvgElectronic, AvgAcoustic, AvgVoice, Interactions, ToleranceMultiplier)
                VALUES (@UserId, @TimeBucket, @AvgBpm, @AvgDanceability, @AvgAggressive, @AvgHappy, @AvgParty, @AvgRelaxed, @AvgSad, @AvgElectronic, @AvgAcoustic, @AvgVoice, @Interactions, @ToleranceMultiplier);";
            
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@TimeBucket", profile.TimeBucket);
            cmd.Parameters.AddWithValue("@AvgBpm", profile.AvgBpm);
            cmd.Parameters.AddWithValue("@AvgDanceability", profile.AvgDanceability);
            cmd.Parameters.AddWithValue("@AvgAggressive", profile.AvgAggressive);
            cmd.Parameters.AddWithValue("@AvgHappy", profile.AvgHappy);
            cmd.Parameters.AddWithValue("@AvgParty", profile.AvgParty);
            cmd.Parameters.AddWithValue("@AvgRelaxed", profile.AvgRelaxed);
            cmd.Parameters.AddWithValue("@AvgSad", profile.AvgSad);
            cmd.Parameters.AddWithValue("@AvgElectronic", profile.AvgElectronic);
            cmd.Parameters.AddWithValue("@AvgAcoustic", profile.AvgAcoustic);
            cmd.Parameters.AddWithValue("@AvgVoice", profile.AvgVoice);
            cmd.Parameters.AddWithValue("@Interactions", profile.Interactions);
            cmd.Parameters.AddWithValue("@ToleranceMultiplier", profile.ToleranceMultiplier);
            cmd.ExecuteNonQuery();
        }

        private AcousticData GetAcousticsForTrack(string itemId)
        {
            var data = new AcousticData { IsValid = false };
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var query = "SELECT Bpm, Danceability, Aggressive, Happy, Party, Relaxed, Sad, Electronic, Acoustic, Voice, Rhythm, Genres, Language FROM AcousticFeatures WHERE ItemId = @ItemId;";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@ItemId", itemId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                data.Bpm = reader.GetDouble(0);
                data.Danceability = reader.GetDouble(1);
                data.Aggressive = reader.GetDouble(2);
                data.Happy = reader.GetDouble(3);
                data.Party = reader.GetDouble(4);
                data.Relaxed = reader.GetDouble(5);
                data.Sad = reader.GetDouble(6);
                data.Electronic = reader.GetDouble(7);
                data.Acoustic = reader.GetDouble(8);
                data.Voice = reader.GetDouble(9);
                data.Rhythm = reader.GetString(10);
                data.Genres = reader.GetString(11);
                data.Language = reader.GetString(12);
                data.IsValid = true;
            }
            return data;
        }

        private AcousticData GetAverageAcoustics(List<string> seedIds)
        {
            var avg = new AcousticData { IsValid = false };
            if (seedIds == null || !seedIds.Any()) return avg;

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var parameters = new string[seedIds.Count];
            var command = new SqliteCommand { Connection = connection };

            for (int i = 0; i < seedIds.Count; i++)
            {
                parameters[i] = $"@p{i}";
                command.Parameters.AddWithValue(parameters[i], seedIds[i]);
            }

            command.CommandText = $@"
                SELECT AVG(Bpm), AVG(Danceability), AVG(Aggressive), AVG(Happy), AVG(Party), AVG(Relaxed), AVG(Sad), AVG(Electronic), AVG(Acoustic), AVG(Voice)
                FROM AcousticFeatures 
                WHERE ItemId IN ({string.Join(",", parameters)});";

            using var reader = command.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                avg.Bpm = reader.GetDouble(0);
                avg.Danceability = reader.GetDouble(1);
                avg.Aggressive = reader.GetDouble(2);
                avg.Happy = reader.GetDouble(3);
                avg.Party = reader.GetDouble(4);
                avg.Relaxed = reader.GetDouble(5);
                avg.Sad = reader.GetDouble(6);
                avg.Electronic = reader.GetDouble(7);
                avg.Acoustic = reader.GetDouble(8);
                avg.Voice = reader.GetDouble(9);
                avg.IsValid = true;
            }
            return avg;
        }

        public bool HasAcousticData(string itemId)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var query = "SELECT COUNT(1) FROM AcousticFeatures WHERE ItemId = @ItemId;";
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public void SaveAcousticData(string itemId, double bpm, double danceability, double aggressive, double happy, double party, double relaxed, double sad, double electronic, double acoustic, double voice, string rhythm, string genres, string language)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            var query = @"
                INSERT OR REPLACE INTO AcousticFeatures 
                (ItemId, Bpm, Danceability, Aggressive, Happy, Party, Relaxed, Sad, Electronic, Acoustic, Voice, Rhythm, Genres, Language)
                VALUES (@ItemId, @Bpm, @Dance, @Agg, @Happy, @Party, @Rel, @Sad, @Elec, @Acoustic, @Voice, @Rhythm, @Genres, @Language);";
            
            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@ItemId", itemId);
            cmd.Parameters.AddWithValue("@Bpm", bpm);
            cmd.Parameters.AddWithValue("@Dance", danceability);
            cmd.Parameters.AddWithValue("@Agg", aggressive);
            cmd.Parameters.AddWithValue("@Happy", happy);
            cmd.Parameters.AddWithValue("@Party", party);
            cmd.Parameters.AddWithValue("@Rel", relaxed);
            cmd.Parameters.AddWithValue("@Sad", sad);
            cmd.Parameters.AddWithValue("@Elec", electronic);
            cmd.Parameters.AddWithValue("@Acoustic", acoustic);
            cmd.Parameters.AddWithValue("@Voice", voice);
            cmd.Parameters.AddWithValue("@Rhythm", rhythm ?? "unknown");
            cmd.Parameters.AddWithValue("@Genres", genres ?? "Unknown");
            cmd.Parameters.AddWithValue("@Language", language ?? "unknown");
            cmd.ExecuteNonQuery();
        }

        // --- NEW ENDPOINT: HEURISTIC MOOD ENGINE ---
        public List<string> GetMoodQueue(string mood, string languageFilter, int limit)
        {
            var results = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            string moodCondition = mood.ToLower() switch
            {
                "uplifting" => "Happy > 0.6 AND Party > 0.6",
                "workout" => "(Aggressive > 0.7 OR Party > 0.7) AND Bpm > 115",
                "happy" => "Happy > 0.75",
                "party" => "Party > 0.75",
                "relax" => "Relaxed > 0.7",
                "sad" => "Sad > 0.75",
                "chill" => "Relaxed > 0.75 AND Bpm < 110",
                "love" => "Happy BETWEEN 0.4 AND 0.8 AND Relaxed > 0.6",
                "romance" => "Relaxed > 0.7 AND Happy BETWEEN 0.3 AND 0.7",
                "sleep" => "Relaxed > 0.85 AND Bpm < 90",
                "dance" => "Danceability > 0.8",
                "sing" => "Voice > 0.75",
                _ => "1=1" 
            };

            var query = $@"
                SELECT ItemId FROM AcousticFeatures 
                WHERE {moodCondition}
                AND (@Lang = 'any' OR Language = @LangSearch)
                ORDER BY RANDOM() 
                LIMIT @Limit;";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.Parameters.AddWithValue("@Lang", string.IsNullOrEmpty(languageFilter) ? "any" : languageFilter.ToLower());
            cmd.Parameters.AddWithValue("@LangSearch", languageFilter?.ToLower() ?? "any");
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) results.Add(reader.GetString(0));
            return results;
        }

        // --- NEW ENDPOINT: GENRE ENGINE ---
        public List<string> GetGenreQueue(string genre, string languageFilter, int limit)
        {
            var results = new List<string>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();

            var query = @"
                SELECT ItemId FROM AcousticFeatures 
                WHERE Genres LIKE @GenreSearch
                AND (@Lang = 'any' OR Language = @LangSearch)
                ORDER BY RANDOM() 
                LIMIT @Limit;";

            using var cmd = new SqliteCommand(query, connection);
            cmd.Parameters.AddWithValue("@Limit", limit);
            cmd.Parameters.AddWithValue("@GenreSearch", $"%{genre}%");
            cmd.Parameters.AddWithValue("@Lang", string.IsNullOrEmpty(languageFilter) ? "any" : languageFilter.ToLower());
            cmd.Parameters.AddWithValue("@LangSearch", languageFilter?.ToLower() ?? "any");
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) results.Add(reader.GetString(0));
            return results;
        }
    }
}