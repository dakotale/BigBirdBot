using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DiscordBot.Constants
{
    /// <summary>
    /// Configuration values loaded from environment variables or a local secrets.json.
    /// Do NOT commit a real secrets.json to source control. Use GitHub repository secrets for CI/runtime.
    /// </summary>
    public static class Constants
    {
            private static readonly Dictionary<string, string> _values = new();

        /// <summary>Preloads secrets.json (if present) once at first access to this class.</summary>
        static Constants()
        {
            try
            {
                // Look for a secrets.json in the app base directory (for local dev).
                var path = Path.Combine(AppContext.BaseDirectory, "secrets.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed is not null)
                    {
                        foreach (var kv in parsed)
                        {
                            // Normalize keys to match property names (case-sensitive use as-is)
                            _values[kv.Key] = kv.Value;
                        }
                    }
                }
            }
            catch
            {
                // Swallow exceptions here to avoid breaking startup if secrets file is unavailable.
            }
        }

        /// <summary>
        /// Resolves a config value by key, checking (in order) the environment variable,
        /// then secrets.json, then the supplied fallback.
        /// </summary>
        private static string Get(string key, string? fallback = null)
        {
            // 1) Check environment variable (use exact key name)
            var env = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(env))
                return env;

            // 2) Check loaded secrets.json values
            if (_values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;

            // 3) Fallback (may be null)
            return fallback ?? string.Empty;
        }

        public static string postgresConnStr => Get(nameof(postgresConnStr));
        public static string botToken => Get(nameof(botToken));
        public static string devBotToken => Get(nameof(devBotToken));
        public static string lavalinkUrl => Get(nameof(lavalinkUrl), "http://localhost:2333");
        public static string lavaLinkPwd => Get(nameof(lavaLinkPwd));
        public static string errorImageUrl => Get(nameof(errorImageUrl), "https://cdn0.iconfinder.com/data/icons/shift-interfaces/32/Error-512.png");
        public static string aiApiUserId => Get(nameof(aiApiUserId));
        public static string aiApiSecretId => Get(nameof(aiApiSecretId));
        public static string aiDetectorPath => Get(nameof(aiDetectorPath), @"C:\Temp\DiscordBot\AIDetector\");
        public static string avatarTempPath => Get(nameof(avatarTempPath), @"C:\Temp\DiscordBot\avatartemp\");
        public static string openAiToken => Get(nameof(openAiToken));
        public static string openAiModel => Get(nameof(openAiModel), "gpt-4.1");
        public static string keywordDirectory => Get(nameof(keywordDirectory), @"C:\Temp\DiscordBot\");
        public static string minecraftModsDirectory => Get(nameof(minecraftModsDirectory), @"C:\Users\Dakota\Desktop\Cobblemon\mods\");

        public static string spotifyClientId => Get(nameof(spotifyClientId), "9d3327c7e115414386b546393c6e935d");

        public static string spotifyClientSecret => Get(nameof(spotifyClientSecret), "e5c19c145b0e4ba68b8b76f3a5acf1b2");
        public static string anthropicApiKey => Get(nameof(anthropicApiKey));
    }
}

