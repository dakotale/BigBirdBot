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

        /// <summary>
        /// Per-user data directory used only for fallback defaults below — the real paths are
        /// set in secrets.json / env vars. Resolves per-OS: <c>%LOCALAPPDATA%</c> on Windows,
        /// <c>~/.local/share</c> on macOS/Linux.
        /// </summary>
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BigBirdBot");

        // PostgreSQL
        public static string discordBotConnStr => Get(nameof(discordBotConnStr), "Host=localhost;Port=5432;Database=discordbot;Username=discordbot");
        public static string botToken => Get(nameof(botToken));
        public static string devBotToken => Get(nameof(devBotToken));
        public static string lavalinkUrl => Get(nameof(lavalinkUrl), "http://localhost:2333");
        public static string lavaLinkPwd => Get(nameof(lavaLinkPwd));
        public static string errorImageUrl => Get(nameof(errorImageUrl), "https://cdn0.iconfinder.com/data/icons/shift-interfaces/32/Error-512.png");
        public static string aiApiUserId => Get(nameof(aiApiUserId));
        public static string aiApiSecretId => Get(nameof(aiApiSecretId));
        public static string aiDetectorPath => Get(nameof(aiDetectorPath), Path.Combine(DataDir, "AIDetector"));
        public static string keywordDirectory => Get(nameof(keywordDirectory), Path.Combine(DataDir, "keywords"));
        public static string spotifyClientId => Get(nameof(spotifyClientId), "9d3327c7e115414386b546393c6e935d");
        public static string spotifyClientSecret => Get(nameof(spotifyClientSecret), "e5c19c145b0e4ba68b8b76f3a5acf1b2");
        public static string anthropicApiKey => Get(nameof(anthropicApiKey));

        /// <summary>
        /// Fixed Discord IDs for the bot developer's own server/channel/account. Previously
        /// copy-pasted as literals in Program.cs, ServerCommands.cs (/reportbug), and the
        /// [GuildModule] on OwnerCommands — centralized here so there is one source of truth.
        /// </summary>
        public static class Bot
        {
            /// <summary>The bot developer's home guild — hosts the log channel and owner-only commands.</summary>
            public const ulong LogGuildId = 880569055856185354UL;

            /// <summary>Channel in <see cref="LogGuildId"/> that exceptions and bug reports are posted to.</summary>
            public const ulong LogChannelId = 1156625507840954369UL;

            /// <summary>The bot owner's user ID (used for scheduler failure DMs and RequireOwner checks).</summary>
            public const ulong OwnerId = 171369791486033920UL;

            /// <summary>The bot's application/user name, as it appears in its managed integration role.</summary>
            public const string Name = "BigBirdBot";
        }
    }
}

