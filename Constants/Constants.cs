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

        public static string discordBotConnStr => Get(nameof(discordBotConnStr), "Server=localhost;DataBase=DiscordBot;Integrated Security=true;TrustServerCertificate=True");
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
    }
}
