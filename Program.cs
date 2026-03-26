using System.Data;
using System.Data.SqlClient;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Services;
using Fergun.Interactive;
using KillersLibrary.Services;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
ConfigureServices(builder.Services);
await builder.Build().Services
             .GetRequiredService<BotHost>()
             .RunAsync();

static void ConfigureServices(IServiceCollection services) =>
    services
        .AddSingleton(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged
                                     | GatewayIntents.MessageContent
                                     | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
            DefaultRetryMode = RetryMode.AlwaysRetry,
            LogGatewayIntentWarnings = false,
            LogLevel = LogSeverity.Verbose
        })
        .AddSingleton<DiscordSocketClient>()
        .AddSingleton<CommandService>()
        .AddSingleton<LoggingService>()
        .AddSingleton<InteractionHandlerService>()
        .AddSingleton<InteractionService>(p =>
            new InteractionService(p.GetRequiredService<DiscordSocketClient>()))
        .AddSingleton(new InteractiveConfig
        {
            DefaultTimeout = TimeSpan.FromMinutes(15),
            LogLevel = LogSeverity.Warning
        })
        .AddSingleton<InteractiveService>()
        .AddSingleton<EmbedPagesService>()
        .AddSingleton<MultiButtonsService>()
        .AddSingleton<BotHost>()
        .AddLavalink()
        .ConfigureLavalink(x =>
        {
            x.BaseAddress = new Uri(Constants.lavalinkUrl);
            x.Passphrase = Constants.lavaLinkPwd;
            x.BufferSize = 2048;
            x.Label = "BigBirdBot";
            x.ReadyTimeout = TimeSpan.FromMinutes(15);
            x.ResumptionOptions = new(TimeSpan.Zero);
        })
        .AddHttpClient()
        .AddSingleton<ISpotifyService, SpotifyService>()
        .AddLogging(x => x.ClearProviders().SetMinimumLevel(LogLevel.Trace));


internal sealed class BotHost(
    DiscordSocketClient client,
    LoggingService logger,
    IServiceProvider services,
    IHttpClientFactory httpClientFactory)
{
    private const ulong LogGuildId = 880569055856185354UL;
    private const ulong LogChannelId = 1156625507840954369UL;
    private const ulong OwnerId = 171369791486033920UL;
    private System.Timers.Timer? _stockTimer;
    private System.Timers.Timer? _stockDayResetTimer;
    private int _schedulerTick = 0;
    private Task? _schedulerTask;

    // ── Per-channel puzzle hint state ──────────────────────────────────────────
    // Shared between the scheduler (creation), T+30/T+50 reveal tasks, and
    // OnMessageReceivedAsync (guess tracking) so all reveals accumulate correctly.

    private sealed class PuzzleHintState
    {
        public readonly string Word;
        public readonly IUserMessage Message;
        private readonly HashSet<int> _revealed = new();
        private int _guessCount;

        public PuzzleHintState(string word, IUserMessage msg)
        {
            Word = word;
            Message = msg;
            _revealed.Add(0); // first letter shown from the start
        }

        /// Tries to reveal one new unrevealed letter.
        /// Returns true and sets <paramref name="hint"/> to the updated string when
        /// a new letter was revealed; returns false when all letters are already shown.
        public bool TryRevealNext(out string hint)
        {
            lock (_revealed)
            {
                var available = Enumerable.Range(1, Word.Length - 1)
                    .Where(i => !_revealed.Contains(i))
                    .ToList();

                if (available.Count == 0)
                {
                    hint = BuildHint();
                    return false;
                }

                int idx = available[Random.Shared.Next(available.Count)];
                _revealed.Add(idx);
                hint = BuildHint();
                return true;
            }
        }

        public string GetCurrentHint()
        {
            lock (_revealed) return BuildHint();
        }

        /// Returns the new total guess count.
        public int IncrementGuesses() => Interlocked.Increment(ref _guessCount);

        private string BuildHint()
        {
            char[] chars = new string('_', Word.Length).ToCharArray();
            foreach (int i in _revealed) chars[i] = Word[i];
            return new string(chars);
        }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PuzzleHintState>
        _puzzleHintStates = new();

    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();
    private readonly DiscordBot.SlashCommands.Economy _creditEco = new();

    private static readonly Dictionary<string, string> EmojiToLetter = new()
    {
        ["🇦"] = "A.",
        ["🇧"] = "B.",
        ["🇨"] = "C.",
        ["🇩"] = "D."
    };


    public async Task RunAsync()
    {
        await services.GetRequiredService<InteractionHandlerService>().InitializeAsync();
        RegisterEvents();
        _schedulerTask = RunSchedulerAsync();
        StartStockTimer();
        await ConnectAsync();
        await Task.Delay(Timeout.Infinite);
    }

    private async Task ConnectAsync()
    {
        try
        {
            await logger.InfoAsync("Starting Bot");
            await client.LoginAsync(TokenType.Bot, Constants.botToken);
            await client.StartAsync();
        }
        catch (Exception ex)
        {
            await ReconnectAsync(ex);
        }
    }

    private async Task ReconnectAsync(Exception ex)
    {
        await logger.InfoAsync($"{ex.GetType().Name}: {ex.Message}");
        try { await client.LogoutAsync(); } catch { /* ignore */ }
        await Task.Delay(TimeSpan.FromSeconds(5));
        await ConnectAsync();
    }

    private void RegisterEvents()
    {
        client.Connected += OnConnectedAsync;
        client.Disconnected += OnDisconnectedAsync;
        client.Log += OnLogMessageAsync;
        client.JoinedGuild += OnJoinedGuildAsync;
        client.UserJoined += OnUserJoinedAsync;
        client.UserLeft += OnUserLeftAsync;
        client.ButtonExecuted += OnButtonExecutedAsync;
        client.MessageReceived += OnMessageReceivedAsync;
        client.ReactionAdded += OnReactionAddedAsync;
        client.UserVoiceStateUpdated += OnUserVoiceStateUpdatedAsync;
    }


    private async Task OnConnectedAsync()
    {
        await logger.InfoAsync("Bot connected");
        await client.SetGameAsync("/reportbug");

        // Restart scheduler if it died while Discord was disconnected.
        if (_schedulerTask is null || _schedulerTask.IsCompleted)
        {
            await logger.InfoAsync("[Scheduler] Restarting scheduler loop after reconnect.");
            _schedulerTask = RunSchedulerAsync();
        }

        // Restart stock timers if they stopped.
        if (_stockTimer is null || !_stockTimer.Enabled)
        {
            await logger.InfoAsync("[StockMarket] Restarting stock timers after reconnect.");
            _stockTimer?.Dispose();
            _stockDayResetTimer?.Dispose();
            StartStockTimer();
        }
    }

    private async Task OnDisconnectedAsync(Exception ex) =>
        await logger.InfoAsync($"Bot disconnected ({client.ConnectionState}): {ex.Message}");

    private async Task OnLogMessageAsync(LogMessage msg)
    {
        if (msg.Exception is null || msg.Message.Length == 0) return;

        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;

        await channel.SendMessageAsync(embed: _embed.BuildMessageEmbed(
            "Exception Thrown",
            $"Exception: {msg.Exception.Message}\nMessage: {msg.Message}",
            "", "BigBirdBot", Color.Red).Build());
    }


    private Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        if (user.IsBot || user.IsWebhook) return Task.CompletedTask;

        _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteUser",
        [
            new SqlParameter("@UserID",   user.Id.ToString()),
            new SqlParameter("@ServerID", guild.Id.ToString())
        ]);
        return Task.CompletedTask;
    }

    private Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot || user.IsWebhook) return Task.CompletedTask;
        AddUserToDatabase(user, user.Guild.Id);
        return Task.CompletedTask;
    }

    private async Task OnJoinedGuildAsync(SocketGuild guild)
    {
        var existingIds = _sp
            .Select(Constants.discordBotConnStr, "GetServers", [])
            .AsEnumerable()
            .Select(r => r["ServerUID"].ToString())
            .ToHashSet();

        if (!existingIds.Contains(guild.Id.ToString()))
        {
            _sp.UpdateCreate(Constants.discordBotConnStr, "AddServer",
            [
                new SqlParameter("@ServerUID",        (long)guild.Id),
                new SqlParameter("@ServerName",       guild.Name),
                new SqlParameter("@DefaultChannelID", (long)guild.DefaultChannel.Id)
            ]);
        }

        await guild.DownloadUsersAsync();

        if (guild.Users.Count == 0)
        {
            await SendLogAsync(
                $"Bot joined **{guild.Name}** but DownloadUsersAsync returned 0 users. Owner: {guild.Owner}",
                Color.Red);
            return;
        }

        foreach (var user in guild.Users.Where(u => !u.IsBot && !u.IsWebhook))
            AddUserToDatabase(user, guild.Id);

        await logger.InfoAsync($"{guild.Users.Count} users added for {guild.Name}");
    }

    private void AddUserToDatabase(SocketGuildUser user, ulong guildId) =>
        _sp.UpdateCreate(Constants.discordBotConnStr, "AddUser",
        [
            new SqlParameter("@UserID",    user.Id.ToString()),
            new SqlParameter("@Username",  user.Username),
            new SqlParameter("@JoinDate",  user.JoinedAt),
            new SqlParameter("@ServerUID", (long)guildId),
            new SqlParameter("@Nickname",  user.Nickname)
        ]);


    private async Task OnButtonExecutedAsync(SocketMessageComponent component)
    {
        if (component.Data.CustomId.Contains('_') || component.Data.CustomId.Contains(':'))
            return;

        var pronounTable = _sp.Select(Constants.discordBotConnStr, "GetPronouns", []);
        string pronounSelected = "";
        var guild = client.GetGuild(component.GuildId!.Value);

        foreach (DataRow row in pronounTable.Rows)
        {
            string name = row["Pronoun"].ToString()!;
            string id = row["ID"].ToString()!;

            if (!guild.Roles.Any(r => r.Name == name))
                await guild.CreateRoleAsync(name);

            if (id == component.Data.CustomId)
                pronounSelected = name;
        }

        guild = client.GetGuild(component.GuildId!.Value);
        var role = guild.Roles.FirstOrDefault(r => r.Name == pronounSelected);
        var guildUser = guild.GetUser(component.User.Id);

        if (role is null) return;

        bool hasRole = guildUser.Roles.Any(r => r.Name == role.Name);

        if (hasRole)
            await ((IGuildUser)guildUser).RemoveRoleAsync(role);
        else
            await ((IGuildUser)guildUser).AddRoleAsync(role);

        string action = hasRole ? "removed" : "added";
        await component.RespondAsync(
            embed: _embed.BuildMessageEmbed(
                "Pronoun Selection",
                $"Pronouns were successfully {action} for {component.User.Username}.",
                "", component.User.Username, Color.Blue).Build(),
            ephemeral: true);
    }


    private async Task OnMessageReceivedAsync(SocketMessage msg)
    {
        if (msg is not { Author.IsBot: false, Author.IsWebhook: false, Channel: SocketGuildChannel msgChannel })
            return;

        string message = msg.Content.Trim().ToLowerInvariant();
        string serverId = msgChannel.Guild.Id.ToString();
        string userId = msg.Author.Id.ToString();
        const string prefix = "-";

        _sp.UpdateCreate(Constants.discordBotConnStr, "UpdateUserLastSeen",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", long.Parse(serverId))
        ]);

        // Passive credits — pass serverId explicitly (Context.Guild is null outside slash commands)
        _creditEco.AddCredits(userId, serverId, CreditHelper.PassiveMessageAmount, "message");

        var serverInfo = _sp.Select(Constants.discordBotConnStr, "GetServerByID",
            [new SqlParameter("ServerUID", long.Parse(serverId))]);

        if (!bool.TryParse(serverInfo.Rows[0]["IsActive"]?.ToString(), out bool active) || !active)
            return;

        var cleanup = new URLCleanup();

        if (cleanup.HasSocialMediaEmbed(message) && !message.StartsWith(prefix))
        {
            var embedSettings = _sp.Select(Constants.discordBotConnStr, "GetEmbedBroken",
                [new SqlParameter("@ServerUID", long.Parse(serverId))]);

            if (bool.TryParse(embedSettings.Rows[0]["FixEmbed"]?.ToString(), out bool fix) && fix)
                await msg.Channel.SendMessageAsync(cleanup.CleanURLEmbed(message));

            return;
        }

        if (message.StartsWith(prefix))
        {
            await HandlePrefixCommandAsync(msg, message, serverId, userId, prefix, cleanup);
            return;
        }


        var activePetRow = _sp.Select(Constants.discordBotConnStr, "GetActivePet",
            [new SqlParameter("@UserID", userId)]);

        if (activePetRow.Rows.Count > 0)
        {
            bool petHibernating = bool.TryParse(
                activePetRow.Rows[0]["IsHibernating"].ToString(), out bool ph) && ph;

            if (!petHibernating)
            {
                int petId = int.Parse(activePetRow.Rows[0]["PetID"].ToString()!);
                int xpGain = DiscordBot.Helper.PetHelper.XpMessage;

                if (msg.Attachments.Count > 0)
                    xpGain += DiscordBot.Helper.PetHelper.XpAttachment;

                if (message.Contains("http://") || message.Contains("https://"))
                    xpGain += DiscordBot.Helper.PetHelper.XpLink;

                var xpResult = _sp.Select(Constants.discordBotConnStr, "AddPetXP",
                [
                    new SqlParameter("@PetID",  petId),
                    new SqlParameter("@Amount", xpGain)
                ]);

                if (xpResult.Rows.Count > 0)
                {
                    int newXp = int.Parse(xpResult.Rows[0]["XP"].ToString()!);
                    int oldXp = newXp - xpGain;
                    int oldLevel = DiscordBot.Helper.PetHelper.LevelFromXp(oldXp);
                    int newLevel = DiscordBot.Helper.PetHelper.LevelFromXp(newXp);

                    if (newLevel > oldLevel)
                    {
                        string petName = activePetRow.Rows[0]["Name"].ToString()!;
                        string species = activePetRow.Rows[0]["Species"].ToString()!;
                        string? unlock = DiscordBot.Helper.PetHelper.LevelUpUnlock(newLevel);
                        string emoji = DiscordBot.Helper.PetHelper.PetEmoji(
                            species, 100, 100, false, newLevel >= 50);

                        decimal lvlBonus = CreditHelper.PetLevelUpAmount(newLevel);
                        decimal newBalance = _creditEco.AddCredits(userId, serverId, lvlBonus, "pet_levelup");

                        await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle($"{emoji}  {petName} levelled up!")
                            .WithColor(new Color(255, 215, 0))
                            .WithDescription(
                                $"{msg.Author.Mention}'s pet **{petName}** is now **Level {newLevel}**! 🎉\n" +
                                $"Bonus: {CreditHelper.Format(lvlBonus)} | Balance: {CreditHelper.Format(newBalance)}" +
                                (unlock is not null ? $"\n\n{unlock}" : ""))
                            .WithCurrentTimestamp()
                            .Build());
                    }
                }
            }
        }


        var scramble = _sp.Select(Constants.discordBotConnStr, "GetScrambleByChannel",
            [new SqlParameter("@ChannelID", msgChannel.Id.ToString())]);

        if (scramble.Rows.Count > 0)
        {
            bool expired = DateTime.TryParse(scramble.Rows[0]["ExpiresAt"].ToString(), out var expiresAt)
                           && DateTime.UtcNow > expiresAt;

            if (!expired)
            {
                string correctAnswer = scramble.Rows[0]["Answer"].ToString()!;

                if (string.Equals(message, correctAnswer, StringComparison.OrdinalIgnoreCase))
                {
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteScrambleGame",
                        [new SqlParameter("@ChannelID", msgChannel.Id.ToString())]);

                    await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("🎉  Correct!")
                        .WithColor(Color.Green)
                        .WithDescription(
                            $"{msg.Author.Mention} solved it! The word was **{correctAnswer}**.")
                        .WithFooter($"Solved by {msg.Author.Username}")
                        .WithCurrentTimestamp()
                        .Build());
                }

                return;
            }
        }


        var petPuzzle = _sp.Select(Constants.discordBotConnStr, "GetActivePetPuzzle",
            [new SqlParameter("@ChannelID", msg.Channel.Id.ToString())]);

        if (petPuzzle.Rows.Count > 0)
        {
            string puzzleWord = petPuzzle.Rows[0]["Word"].ToString()!;
            int puzzleId = int.Parse(petPuzzle.Rows[0]["PuzzleID"].ToString()!);

            string puzzleChannelId = msg.Channel.Id.ToString();

            if (string.Equals(message.Trim(), puzzleWord, StringComparison.OrdinalIgnoreCase))
            {
                // Clean up shared hint state for this channel
                _puzzleHintStates.TryRemove(puzzleChannelId, out _);

                _sp.UpdateCreate(Constants.discordBotConnStr, "ClaimPetPuzzle",
                    [new SqlParameter("@PuzzleID", puzzleId)]);

                // Always award credits for solving the puzzle.
                _creditEco.AddCredits(userId, serverId, CreditHelper.PuzzleSolveAmount, "puzzle");

                var solverPet = _sp.Select(Constants.discordBotConnStr, "GetActivePet",
                    [new SqlParameter("@UserID", userId)]);

                bool awardedXp = false;
                string petLine  = string.Empty;

                if (solverPet.Rows.Count > 0)
                {
                    bool solverHib = bool.TryParse(
                        solverPet.Rows[0]["IsHibernating"].ToString(), out bool sh) && sh;

                    if (!solverHib)
                    {
                        int    solverPetId   = int.Parse(solverPet.Rows[0]["PetID"].ToString()!);
                        string solverPetName = solverPet.Rows[0]["Name"].ToString()!;

                        _sp.Select(Constants.discordBotConnStr, "AddPetXP",
                        [
                            new SqlParameter("@PetID",  solverPetId),
                            new SqlParameter("@Amount", DiscordBot.Helper.PetHelper.XpWordPuzzle)
                        ]);

                        awardedXp = true;
                        petLine   = $"\n**{solverPetName}** earned **+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP**! 🐾";
                    }
                }

                string description = awardedXp
                    ? $"{msg.Author.Mention} solved the bonus word puzzle!\n" +
                      $"They earned {CreditHelper.Format(CreditHelper.PuzzleSolveAmount)}!{petLine} 🎉"
                    : $"{msg.Author.Mention} solved the bonus word puzzle!\n" +
                      $"They earned {CreditHelper.Format(CreditHelper.PuzzleSolveAmount)}! 🎉";

                await msg.Channel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithTitle("🧩  Puzzle Solved!")
                    .WithColor(Color.Green)
                    .WithDescription(description)
                    .WithCurrentTimestamp()
                    .Build());

                return;
            }

            // ── Every-20-guesses letter reveal ────────────────────────────────
            // Count any single-word alphabetic attempt (wrong answers only —
            // correct answers are handled and returned above).
            string trimmedGuess = message.Trim();
            bool isWordAttempt  = trimmedGuess.Length > 0 && trimmedGuess.All(char.IsLetter);

            if (isWordAttempt && _puzzleHintStates.TryGetValue(puzzleChannelId, out var guessState))
            {
                int totalGuesses = guessState.IncrementGuesses();
                if (totalGuesses % 20 == 0 && guessState.TryRevealNext(out string guessHint))
                {
                    try
                    {
                        await guessState.Message.ModifyAsync(m => m.Embed = new EmbedBuilder()
                            .WithTitle("🧩  Bonus Word Puzzle!")
                            .WithColor(new Color(255, 179, 71))
                            .WithDescription(
                                $"Type the secret word in this channel to earn " +
                                $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                                $"**Hint:** `{guessHint}`  ({guessState.Word.Length} letters)\n" +
                                $"*(A letter was revealed after {totalGuesses} guesses!)*\n\n" +
                                $"⏳ First correct answer wins!")
                            .WithCurrentTimestamp()
                            .Build());
                    }
                    catch { /* message may have been deleted */ }
                }
            }
        }


        if (message.Length == 5 && message.All(char.IsLetter))
        {
            var wordle = _sp.Select(Constants.discordBotConnStr, "GetWordleByChannel",
                [new SqlParameter("@ChannelID", msgChannel.Id.ToString())]);

            if (wordle.Rows.Count > 0)
            {
                string answer = wordle.Rows[0]["Answer"].ToString()!;
                string messageIdStr = wordle.Rows[0]["MessageID"].ToString()!;
                string guessesRaw = wordle.Rows[0]["Guesses"].ToString()!;

                var guesses = string.IsNullOrEmpty(guessesRaw)
                    ? new List<string>()
                    : guessesRaw.Split(',').ToList();

                guesses.Add(message);

                bool won = message.Equals(answer, StringComparison.OrdinalIgnoreCase);
                bool gameOver = won || guesses.Count >= 6;

                string newGuesses = string.Join(",", guesses);

                if (gameOver)
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteWordleGame",
                        [new SqlParameter("@ChannelID", msgChannel.Id.ToString())]);
                else
                    _sp.UpdateCreate(Constants.discordBotConnStr, "UpdateWordleGame",
                    [
                        new SqlParameter("@ChannelID", msgChannel.Id.ToString()),
                        new SqlParameter("@Guesses",   newGuesses)
                    ]);

                if (ulong.TryParse(messageIdStr, out ulong messageId) &&
                    await msg.Channel.GetMessageAsync(messageId) is IUserMessage gameMsg)
                {
                    await gameMsg.ModifyAsync(m =>
                        m.Embed = DiscordBot.SlashCommands.Games
                            .BuildWordleEmbed(answer, guesses, gameOver).Build());
                }

                return;
            }
        }


        var actions = _sp.Select(Constants.discordBotConnStr, "GetChatAction",
        [
            new SqlParameter("@ServerID", long.Parse(serverId)),
            new SqlParameter("@Message",  message)
        ]);

        if (actions.Rows.Count > 0)
            _ = Task.Run(() => SendChatActionsAsync(msg, msgChannel, actions));
    }

    private async Task HandlePrefixCommandAsync(
        SocketMessage msg, string message, string serverId,
        string userId, string prefix, URLCleanup cleanup)
    {
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string keyword = parts[0][prefix.Length..];
        var keywordMap = _sp.Select(Constants.discordBotConnStr, "GetChatKeywordMap",
            [new SqlParameter("@AddKeyword", keyword)]);

        if (keywordMap.Rows.Count == 0) return;

        if (msg.Attachments.Count > 0)
        {
            await AddAttachmentsAsync(msg, keywordMap.Rows[0]["Keyword"].ToString()!,
                                      Constants.discordBotConnStr, userId);
            await msg.Channel.SendMessageAsync(
                embed: MakeEmbed("Added Image", Color.Blue, "Added attachment(s) successfully.").Build());
        }

        if (parts.Length <= 1) return;

        string content = message[(prefix.Length + keyword.Length)..].Trim();
        bool isMultiUrl = content.Contains(',') && content.Contains("http");

        if (isMultiUrl)
        {
            foreach (string url in content.Split(',', StringSplitOptions.TrimEntries))
            {
                if (!url.StartsWith("http"))
                {
                    await msg.Channel.SendMessageAsync(
                        embed: MakeEmbed("Error", Color.Red, $"Invalid URL: *{url}*").Build());
                    continue;
                }

                string storeValue = await TrySaveSocialImageAsync(url, keyword)
                                    ?? cleanup.CleanURLEmbed(url);
                StoreChatKeyword(keywordMap, storeValue, userId);
            }

            await msg.Channel.SendMessageAsync(
                embed: MakeEmbed("Added Image", Color.Blue, "Added link(s) successfully.").Build());
        }
        else
        {
            string storeValue = await TrySaveSocialImageAsync(content, keyword)
                                ?? cleanup.CleanURLEmbed(content);

            StoreChatKeyword(keywordMap, storeValue, userId);

            string confirmation = storeValue.StartsWith(@"C:\")
                ? "Image downloaded and saved locally."
                : "Added URL/Text successfully.";

            await msg.Channel.SendMessageAsync(
                embed: MakeEmbed("Added URL/Text", Color.Blue, confirmation).Build());
        }
    }

    private void StoreChatKeyword(DataTable keywordMap, string value, string userId)
    {
        foreach (DataRow row in keywordMap.Rows)
        {
            _sp.UpdateCreate(Constants.discordBotConnStr, "AddChatKeyword",
            [
                new SqlParameter("@FilePath",  value),
                new SqlParameter("@TableName", row["Keyword"].ToString()),
                new SqlParameter("@UserID",    userId)
            ]);
        }
    }

    private async Task SendChatActionsAsync(SocketMessage msg, SocketGuildChannel msgChannel, DataTable actions)
    {
        if (client.GetChannel(msgChannel.Id) is not IMessageChannel sender) return;

        foreach (DataRow row in actions.Rows)
        {
            string chatAction = row["ChatAction"].ToString()!;
            string keyword = row["Keyword"].ToString()!;
            bool isNsfw = bool.TryParse(row["NSFW"]?.ToString(), out bool n) && n;

            if (string.IsNullOrWhiteSpace(chatAction)) continue;

            await msg.Channel.TriggerTypingAsync();

            keyword = char.ToUpperInvariant(keyword[0]) + keyword[1..];

            if (chatAction.StartsWith(@"C:\"))
            {
                bool isSpoiler = isNsfw && !chatAction.Contains("SPOILER_");
                var embed = new EmbedBuilder()
                    .WithTitle(keyword)
                    .WithImageUrl("attachment://" + Path.GetFileName(chatAction))
                    .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                    .Build();

                await using var stream = File.OpenRead(chatAction);
                var output = await msg.Channel.SendFileAsync(
                    stream, Path.GetFileName(chatAction), embed: embed, isSpoiler: isSpoiler);

                if (!isSpoiler)
                    await output.AddReactionAsync(new Emoji("❌"));
            }
            else if (chatAction.Contains("http"))
            {
                if (await IsLinkWorkingAsync(chatAction))
                {
                    var embed = new EmbedBuilder()
                        .WithTitle(msg.Content)
                        .WithImageUrl(chatAction)
                        .WithColor(isNsfw ? Color.DarkRed : Color.Blue)
                        .Build();

                    var output = await msg.Channel.SendMessageAsync(embed: embed);
                    if (!isNsfw)
                        await output.AddReactionAsync(new Emoji("❌"));
                }
                else
                {
                    await sender.SendMessageAsync($"Link was dead so I deleted it :) -> {chatAction}");
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteChatKeywordURL",
                    [
                        new SqlParameter("@FilePath", chatAction),
                        new SqlParameter("@Keyword",  "")
                    ]);
                }
            }
            else
            {
                string display = isNsfw ? $"||{chatAction}||" : chatAction;
                var output = await sender.SendMessageAsync(display);
                if (!isNsfw)
                    await output.AddReactionAsync(new Emoji("❌"));
            }
        }
    }


    private async Task OnUserVoiceStateUpdatedAsync(
        SocketUser user, SocketVoiceState before, SocketVoiceState after)
    {
        var guild = before.VoiceChannel?.Guild ?? after.VoiceChannel?.Guild;
        if (guild is null) return;

        SqlParameter[] serverParam = [new SqlParameter("@ServerID", guild.Id.ToString())];

        async Task DisconnectBotsAsync(SocketVoiceChannel channel)
        {
            foreach (var bot in channel.ConnectedUsers.Where(u => u.IsBot))
                await bot.VoiceChannel.DisconnectAsync();
        }

        if (user.IsBot)
        {
            if (after.VoiceChannel is null && before.VoiceChannel is not null)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", [.. serverParam]);
            }
        }
        else if (before.VoiceChannel is not null && after.VoiceChannel is null)
        {
            bool anyNonBotRemaining = before.VoiceChannel.ConnectedUsers.Any(u => !u.IsBot);
            if (!anyNonBotRemaining)
            {
                await DisconnectBotsAsync(before.VoiceChannel);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeletePlayerConnected", [.. serverParam]);
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteMusicQueueAll", [.. serverParam]);
            }
        }
    }


    private async Task OnReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction)
    {
        var download = await cachedMsg.GetOrDownloadAsync();
        if (download is null) return;
        if (client.GetUser(reaction.UserId)?.IsBot == true) return;

        var imageUrl = download.Embeds.FirstOrDefault(e => e.Image.HasValue)?.Image?.Url;

        if (reaction.Emote.Name == "❌" && download.Author.IsBot && download.Reactions.Count < 2)
        {
            string? fileName = imageUrl is not null
                ? Path.GetFileName(new Uri(imageUrl).LocalPath)
                : null;

            if (!string.IsNullOrEmpty(fileName))
            {
                await TryMarkNsfwAsync(fileName, cachedChannel, reaction);
                return;
            }
        }

        if (IsTriviaEmoji(reaction.Emote.Name))
            await HandleTriviaReactionAsync(cachedMsg, cachedChannel, reaction, download);
    }

    private static bool IsTriviaEmoji(string name) =>
        name is "🇦" or "🇧" or "🇨" or "🇩";

    private async Task TryMarkNsfwAsync(
        string content,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        var existing = _sp.Select(Constants.discordBotConnStr, "GetKeywordNSFW",
            [new SqlParameter("@Message", content)]);

        if (existing.AsEnumerable().Any(r => r["NSFW"].ToString() == "1")) return;

        var result = _sp.Select(Constants.discordBotConnStr, "MarkKeywordNSFW",
            [new SqlParameter("@Message", content)]);

        if (result.Rows.Count > 0)
        {
            await channel.Value.SendMessageAsync(embed: _embed.BuildMessageEmbed(
                "NSFW",
                $"Thanks {reaction.User.Value.Mention}, the message was marked as NSFW, sorry about that :)",
                "", "BigBirdBot", Color.Blue).Build());
        }
    }

    private async Task HandleTriviaReactionAsync(
        Cacheable<IUserMessage, ulong> cachedMsg,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction,
        IUserMessage download)
    {
        try
        {
            if (download.Embeds.Count == 0) return;

            long messageId = (long)cachedMsg.Id;
            string userMention = reaction.User.Value.Mention;

            var dt = _sp.Select(Constants.discordBotConnStr, "GetTriviaMessage",
                [new SqlParameter("@TriviaMessageID", messageId)]);

            if (dt.Rows.Count == 0) return;

            string correctAnswer = dt.Rows[0]["CorrectAnswer"].ToString()!;

            var fields = download.Embeds
                .SelectMany(e => e.Fields)
                .Where(f => f.Name.Contains('.'))
                .ToList();

            var correctField = fields.FirstOrDefault(f => f.Value == correctAnswer);
            if (correctField == default
                || !EmojiToLetter.TryGetValue(reaction.Emote.Name, out string? selectedLetter))
                return;

            bool isCorrect = selectedLetter == correctField.Name;

            await channel.Value.SendMessageAsync(embed: new EmbedHelper().BuildMessageEmbed(
                isCorrect ? "Correct" : "Wrong",
                isCorrect
                    ? $"{userMention} answered correctly with **{correctAnswer}**!"
                    : $"{userMention}, you didn't answer correctly. Try again!",
                "", "BigBirdBot",
                isCorrect ? Color.Green : Color.Red).Build());

            if (isCorrect)
                _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteTriviaMessage",
                    [new SqlParameter("@TriviaMessageID", messageId)]);
        }
        catch (Exception ex)
        {
            await channel.Value.SendMessageAsync(embed: new EmbedHelper()
                .BuildMessageEmbed("Error", ex.Message, Constants.errorImageUrl, "", Color.Red).Build());
        }
    }


    private async Task RunSchedulerAsync()
    {
        // Wait until the top of the next minute so every tick lands on a clock minute
        var now = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(60 - now.Second));

        // Seed the counter to the current UTC minute so modulo checks align to real
        // clock boundaries: % 15 → :00/:15/:30/:45, % 30 → :00/:30, % 60 → :00
        _schedulerTick = DateTime.UtcNow.Minute;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync())
        {
            _schedulerTick++;

            // ── Single outer try-catch: any unhandled exception in any block
            //    logs to the owner and lets the loop continue next tick. ──────
            try
            {
                await RunScheduledKeywordsAsync();

                // ── Reminders (every tick = every minute) ───────────────────
                var dueReminders = _sp.Select(Constants.discordBotConnStr, "GetDueReminders", []);
                foreach (DataRow reminderRow in dueReminders.Rows)
                {
                    try
                    {
                        ulong userId = ulong.Parse(reminderRow["UserID"].ToString()!);
                        string message = reminderRow["Message"].ToString()!;
                        var reminderUser = await client.GetUserAsync(userId)
                                        ?? await client.Rest.GetUserAsync(userId);
                        if (reminderUser is null) continue;

                        var dm = await reminderUser.CreateDMChannelAsync();
                        await dm.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle("⏰  Reminder")
                            .WithColor(Color.Gold)
                            .WithDescription(message)
                            .WithFooter("You asked me to remind you at this time.")
                            .WithCurrentTimestamp()
                            .Build());
                    }
                    catch { /* DMs disabled or user not found */ }
                }

            if (_schedulerTick % 30 == 0)
            {
                var decayed = _sp.Select(Constants.discordBotConnStr, "DecayPetStats", []);

                foreach (DataRow decayRow in decayed.Rows)
                {
                    try
                    {
                        ulong ownerId = ulong.Parse(decayRow["UserID"].ToString()!);
                        var owner = await client.GetUserAsync(ownerId);
                        if (owner is null) continue;

                        string petName = decayRow["Name"].ToString()!;
                        string species = decayRow["Species"].ToString()!;

                        await owner.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle("💤  Your pet is hibernating!")
                            .WithColor(Color.DarkGrey)
                            .WithDescription(
                                $"**{petName}** the {species} has gone into hibernation.\n\n" +
                                $"They were too hungry, unhappy, and tired while you were away.\n\n" +
                                $"Use `/feed` to wake them up! Don't worry — they're safe. 🌿")
                            .WithCurrentTimestamp()
                            .Build());
                    }
                    catch { /* DMs disabled or user not found */ }
                }
            }

            if (_schedulerTick % 15 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    await guild.DownloadUsersAsync();

                    foreach (var guildUser in guild.Users.Where(u => !u.IsBot))
                    {
                        bool hasActivity = guildUser.Activities?.Any(a =>
                            a.Type is ActivityType.Playing
                                   or ActivityType.Listening
                                   or ActivityType.Streaming) == true;

                        if (!hasActivity) continue;

                        var userPet = _sp.Select(Constants.discordBotConnStr, "GetActivePet",
                            [new SqlParameter("@UserID", guildUser.Id.ToString())]);

                        if (userPet.Rows.Count == 0) continue;

                        var petRow = userPet.Rows[0];

                        bool petHib = bool.TryParse(
                            petRow["IsHibernating"].ToString(), out bool phibb) && phibb;
                        if (petHib) continue;

                        int petHunger = int.Parse(petRow["Hunger"].ToString()!);
                        if (petHunger <= 20) continue;

                        int activityPetId = int.Parse(petRow["PetID"].ToString()!);

                        _sp.Select(Constants.discordBotConnStr, "AddPetXP",
                        [
                            new SqlParameter("@PetID",  activityPetId),
                            new SqlParameter("@Amount", DiscordBot.Helper.PetHelper.XpActivity)
                        ]);
                    }
                }
            }

            if (_schedulerTick % 60 == 0)
            {
                // Pull a random word from the Words table
                var wordDt = _sp.Select(Constants.discordBotConnStr, "GetRandomWord", []);
                if (wordDt.Rows.Count == 0) goto skipPuzzle;
                string puzzleWord = wordDt.Rows[0]["Word"].ToString()!.Trim();
                if (string.IsNullOrWhiteSpace(puzzleWord)) goto skipPuzzle;

                foreach (var guild in client.Guilds)
                {
                    var serverDetails = ServerHelper.GetServerInfo(guild.Id);
                    if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;
                    if (string.IsNullOrWhiteSpace(serverDetails.DefaultChannelID)) continue;
                    var channel = guild.GetTextChannel(UInt64.Parse(serverDetails.DefaultChannelID));
                    if (channel is null) continue;

                    _sp.UpdateCreate(Constants.discordBotConnStr, "AddPetWordPuzzle",
                    [
                        new SqlParameter("@ChannelID", serverDetails.DefaultChannelID),
                        new SqlParameter("@Word",      puzzleWord),
                        new SqlParameter("@ExpiresAt", DateTime.UtcNow.AddMinutes(55))
                    ]);

                    string blankHint = $"{puzzleWord[0]}{new string('_', puzzleWord.Length - 1)}";

                    var puzzleMsg = await channel.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("🧩  Bonus Word Puzzle!")
                        .WithColor(new Color(255, 179, 71))
                        .WithDescription(
                            $"Type the secret word in this channel to earn " +
                            $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                            $"**Hint:** `{blankHint}`  ({puzzleWord.Length} letters)\n\n" +
                            $"⏳ Expires in 55 minutes — first correct answer wins!")
                        .WithCurrentTimestamp()
                        .Build());

                    // Register shared hint state — all reveal sources (T+30, T+50, every-20-guesses)
                    // accumulate into this so the hint only ever grows, never resets.
                    string capturedChannelId = serverDetails.DefaultChannelID;
                    var hintState = new PuzzleHintState(puzzleWord, puzzleMsg);
                    _puzzleHintStates[capturedChannelId] = hintState;

                    var capturedMsg  = puzzleMsg;
                    var capturedWord = puzzleWord;
                    var capturedCh   = channel;

                    // ── 30-min hint: reveal a second letter ──────────────────
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30));

                        var stillActive = _sp.Select(Constants.discordBotConnStr, "GetPetWordPuzzle",
                            [new SqlParameter("@ChannelID", capturedCh.Id.ToString())]);
                        if (stillActive.Rows.Count == 0) return;

                        if (!hintState.TryRevealNext(out string hint30)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                                .WithTitle("🧩  Bonus Word Puzzle!")
                                .WithColor(new Color(255, 179, 71))
                                .WithDescription(
                                    $"Type the secret word in this channel to earn " +
                                    $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                                    $"**Hint:** `{hint30}`  ({capturedWord.Length} letters)\n" +
                                    $"*(A letter has been revealed!)*\n\n" +
                                    $"⏳ Expires in ~25 minutes — first correct answer wins!")
                                .WithCurrentTimestamp()
                                .Build());
                        }
                        catch { /* message may have been deleted */ }
                    });

                    // ── 50-min hint: reveal a third letter (5-min warning) ───
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(50));

                        var stillActive = _sp.Select(Constants.discordBotConnStr, "GetPetWordPuzzle",
                            [new SqlParameter("@ChannelID", capturedCh.Id.ToString())]);
                        if (stillActive.Rows.Count == 0) return;

                        if (!hintState.TryRevealNext(out string hint50)) return; // all letters already shown

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                                .WithTitle("🧩  Bonus Word Puzzle — Last Chance!")
                                .WithColor(new Color(255, 120, 40))
                                .WithDescription(
                                    $"Type the secret word in this channel to earn " +
                                    $"**+{DiscordBot.Helper.PetHelper.XpWordPuzzle} XP** for your active pet!\n\n" +
                                    $"**Hint:** `{hint50}`  ({capturedWord.Length} letters)\n" +
                                    $"*(Another letter has been revealed!)*\n\n" +
                                    $"⏳ Only **5 minutes** left — first correct answer wins!")
                                .WithCurrentTimestamp()
                                .Build());
                        }
                        catch { /* message may have been deleted */ }
                    });

                    // ── 55-min reveal: show the answer when the puzzle expires ─
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(55));

                        _puzzleHintStates.TryRemove(capturedChannelId, out _);

                        // GetPetWordPuzzle filters ExpiresAt > NOW — use GetPuzzleClaimedStatus
                        // instead so we can distinguish solved vs expired-unsolved.
                        var statusDt = _sp.Select(Constants.discordBotConnStr, "GetPuzzleClaimedStatus",
                            [new SqlParameter("@ChannelID", capturedCh.Id.ToString())]);

                        if (statusDt.Rows.Count > 0
                            && bool.TryParse(statusDt.Rows[0]["Claimed"].ToString(), out bool wasClaimed)
                            && wasClaimed)
                            return;

                        try
                        {
                            await capturedMsg.ModifyAsync(m => m.Embed = new EmbedBuilder()
                                .WithTitle("🧩  Puzzle Expired — No One Got It!")
                                .WithColor(new Color(150, 150, 150))
                                .WithDescription(
                                    $"Time's up! Nobody guessed the word.\n\n" +
                                    $"The answer was: **{capturedWord}**\n\n" +
                                    $"Better luck next time! 🕐")
                                .WithCurrentTimestamp()
                                .Build());
                        }
                        catch { /* message may have been deleted */ }
                    });
                }

                skipPuzzle:;
            }

            if (_schedulerTick % 60 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    var potDt = _sp.Select(Constants.discordBotConnStr, "GetJackpotTotal",
                        [new SqlParameter("@ServerID", guild.Id.ToString())]);

                    if (potDt.Rows.Count == 0) continue;
                    long pot = long.Parse(potDt.Rows[0]["Total"].ToString()!);
                    int entries = int.Parse(potDt.Rows[0]["Entries"].ToString()!);

                    if (pot <= 0 || entries == 0) continue;

                    var serverDetails = ServerHelper.GetServerInfo(guild.Id);
                    if (serverDetails is null || !serverDetails.AnnouncementsEnabled) continue;

                    var entryDt = _sp.Select(Constants.discordBotConnStr, "GetJackpotEntries",
                    [new SqlParameter("@ServerID", guild.Id.ToString())]);

                    if (entryDt.Rows.Count == 0) continue;

                    long totalWeight = entryDt.AsEnumerable()
                        .Sum(r => long.Parse(r["TotalContributed"].ToString()!));

                    long roll = (long)(Random.Shared.NextDouble() * totalWeight);
                    long cum = 0;
                    string? winnerId = null;

                    foreach (System.Data.DataRow eRow in entryDt.Rows)
                    {
                        cum += long.Parse(eRow["TotalContributed"].ToString()!);
                        if (roll < cum) { winnerId = eRow["UserID"].ToString()!; break; }
                    }

                    winnerId ??= entryDt.Rows[0]["UserID"].ToString()!;

                    var _eco = new DiscordBot.SlashCommands.Economy();
                    _eco.AddCredits(winnerId, guild.Id.ToString(), pot, "jackpot_win");

                    _sp.UpdateCreate(Constants.discordBotConnStr, "ClearJackpot",
                        [new SqlParameter("@ServerID", guild.Id.ToString())]);

                    if (string.IsNullOrWhiteSpace(serverDetails.DefaultChannelID)) continue;
                    var channel = guild.GetTextChannel(UInt64.Parse(serverDetails.DefaultChannelID));
                    if (channel is null) continue;

                    IUser? winner = null;
                    try { winner = await client.GetUserAsync(ulong.Parse(winnerId)); } catch { }

                    string winnerDisplay = winner is not null ? winner.Mention : $"<@{winnerId}>";

                    await channel.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("🎰  Jackpot Winner!")
                        .WithColor(new Color(255, 215, 0))
                        .WithDescription(
                            $"🎉 {winnerDisplay} won the jackpot!\n\n" +
                            $"💰 **Prize:** {CreditHelper.Format(pot)}\n" +
                            $"🎟️ **Entries this round:** {entries}\n\n" +
                            $"*The jackpot resets now — use `/jackpot` to enter the next round!*\n" +
                            $"*The jackpot will also add 1% of all gambling bets to the next round!*")
                        .WithCurrentTimestamp()
                        .Build());
                }
            }

            // ── Passive jackpot hourly draw ────────────────────────────────────────
            if (_schedulerTick % 60 == 0)
            {
                foreach (var guild in client.Guilds)
                {
                    var drawDt = _sp.Select(Constants.discordBotConnStr, "DrawPassiveJackpot",
                        [new SqlParameter("@ServerID", (long)guild.Id)]);

                    if (drawDt.Rows.Count == 0) continue; // pool empty or no contributors

                    string passiveWinnerId = drawDt.Rows[0]["UserID"].ToString()!;
                    decimal passivePool    = decimal.Parse(drawDt.Rows[0]["Pool"].ToString()!);

                    var passiveEco = new DiscordBot.SlashCommands.Economy();
                    passiveEco.AddCredits(passiveWinnerId, guild.Id.ToString(), passivePool, "passive_jackpot_win");

                    // Announce in the server's announcement channel (if configured and enabled).
                    var passiveDetails = ServerHelper.GetServerInfo(guild.Id);
                    if (passiveDetails is null || !passiveDetails.AnnouncementsEnabled) continue;

                    ITextChannel? passiveChan = null;
                    if (ulong.TryParse(passiveDetails.DefaultChannelID, out ulong pChanId) && pChanId != 0)
                        passiveChan = guild.GetTextChannel(pChanId);
                    if (passiveChan is null) continue;

                    IUser? passiveWinner = null;
                    try { passiveWinner = await client.GetUserAsync(ulong.Parse(passiveWinnerId)); } catch { }
                    string passiveDisplay = passiveWinner is not null ? passiveWinner.Mention : $"<@{passiveWinnerId}>";

                    await passiveChan.SendMessageAsync(embed: new EmbedBuilder()
                        .WithTitle("🌊  Passive Jackpot Winner!")
                        .WithColor(new Color(100, 200, 255))
                        .WithDescription(
                            $"🎉 {passiveDisplay} won the **passive jackpot** and took home **{DiscordBot.Helper.CreditHelper.Format(passivePool)}**!\n\n" +
                            $"*1% of every gambling bet feeds this pool — keep playing to build it back up!*")
                        .WithCurrentTimestamp()
                        .Build());
                }
            }
            } // end outer try
            catch (Exception ex)
            {
                await NotifyOwnerAsync($"[Scheduler] Tick {_schedulerTick} failed:\n{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private async Task RunScheduledKeywordsAsync()
    {
        System.Data.DataTable dt;
        try
        {
            dt = _sp.Select(Constants.discordBotConnStr, "GetUsersScheduledKeyword", []);
        }
        catch (Exception ex)
        {
            await NotifyOwnerAsync($"[Keywords] SP call failed: {ex.Message}");
            return;
        }

        if (dt.Rows.Count == 0) return;

        foreach (DataRow row in dt.Rows)
        {
            string userId = row["UserID"].ToString()!;
            string filePath = row["FilePath"].ToString()!;
            string tableName = row["ThirstTable"].ToString()!;
            tableName = char.ToUpperInvariant(tableName[0]) + tableName[1..];
            string timestamp = DateTime.Now.ToString("MM/dd/yyyy hh:mm tt ET");

            try
            {
                // GetUserAsync only checks the socket cache; fall back to REST so
                // users who haven't recently interacted with the bot are still resolved.
                ulong uid = ulong.Parse(userId);
                IUser? user = client.GetUser(uid)
                           ?? (IUser?)await client.Rest.GetUserAsync(uid);

                if (user is null)
                {
                    await NotifyOwnerAsync($"[Keywords] Could not resolve user {userId} — skipping tick.");
                    continue;
                }

                if (filePath.StartsWith(@"C:\"))
                {
                    if (File.Exists(filePath))
                        await user.SendFileAsync(filePath, $"**{tableName} - {timestamp}**");
                    else
                        _sp.Select(Constants.discordBotConnStr, "UpdateUsersScheduledKeywordRequeue", [new SqlParameter("@UserID", userId)]);
                }

                else if (await IsLinkWorkingAsync(filePath))
                    await user.SendMessageAsync($"**{tableName} - {timestamp}**\n**URL:** {filePath}");
                else
                {
                    _sp.UpdateCreate(Constants.discordBotConnStr, "DeleteChatKeywordURL",
                    [
                        new SqlParameter("@FilePath", filePath),
                        new SqlParameter("@Keyword",  "")
                    ]);
                    await user.SendMessageAsync(
                        $"**{tableName} - {timestamp}**\n**URL:** {filePath} — dead link removed.");
                }
            }
            catch (HttpException ex)
            {
                await NotifyOwnerAsync(
                    $"DM failed for user {userId} — they may have DMs disabled.\n{ex.Message}");
            }
            catch (Exception ex)
            {
                _sp.UpdateCreate(Constants.discordBotConnStr, "UpdateUsersScheduledKeywordRequeue",
                    [new SqlParameter("@UserID", userId)]);
                await NotifyOwnerAsync(
                    $"Scheduled send failed for user {userId}.\n{ex.StackTrace}\n" +
                    $"Requeued for {DateTime.Now.AddMinutes(1):yyyy-MM-dd hh:mm tt}.");
            }
        }
    }


    private void StartStockTimer()
    {
        // Price tick every 15 minutes
        _stockTimer = new System.Timers.Timer(
            TimeSpan.FromMinutes(StockHelper.TickIntervalMinutes).TotalMilliseconds);
        _stockTimer.Elapsed += (_, _) => TickStockPrices();
        _stockTimer.AutoReset = true;
        _stockTimer.Start();

        // 24h high/low reset — fire at next midnight UTC, then every 24h
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        double initialDelay = (nextMidnight - now).TotalMilliseconds;

        _stockDayResetTimer = new System.Timers.Timer(initialDelay);
        _stockDayResetTimer.Elapsed += (_, _) =>
        {
            ResetStockDayRange();
            _stockDayResetTimer!.Interval = TimeSpan.FromHours(24).TotalMilliseconds;
            _stockDayResetTimer.AutoReset = true;
        };
        _stockDayResetTimer.AutoReset = false;
        _stockDayResetTimer.Start();

        Console.WriteLine(
            $"[StockMarket] Timers started — tick every {StockHelper.TickIntervalMinutes} min, " +
            $"day reset at {nextMidnight:HH:mm} UTC.");
    }

    private void TickStockPrices()
    {
        try
        {
            // Clean expired shop effects on every tick (every 15 min)
            try { _sp.UpdateCreate(Constants.discordBotConnStr, "CleanExpiredEffects", []); }
            catch { /* non-fatal */ }

            var dt = _sp.Select(Constants.discordBotConnStr, "GetAllStocks", []);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                string ticker = row["Ticker"].ToString()!;
                decimal price = decimal.Parse(row["Price"].ToString()!);

                // Per-stock volatility and trend — both columns are now in GetAllStocks
                double volatility = double.Parse(row["Volatility"].ToString()!);
                double trend = double.Parse(row["Trend"].ToString()!);

                decimal newPrice = StockHelper.NextPrice(price, volatility, trend);

                _sp.UpdateCreate(Constants.discordBotConnStr, "ApplyStockTick",
                [
                    new SqlParameter("@Ticker",   ticker),
                    // Explicitly typed to match DECIMAL(18,2) in ApplyStockTick.
                    // Without explicit Precision/Scale ADO.NET infers them as 0,0
                    // and SQL Server raises "Error converting data type numeric to decimal".
                    new SqlParameter("@NewPrice", System.Data.SqlDbType.Decimal)
                    {
                        Value     = newPrice,
                        Precision = 18,
                        Scale     = 2
                    }
                ]);
            }

            Console.WriteLine(
                $"[StockMarket] Tick at {DateTime.UtcNow:HH:mm:ss} UTC — {dt.Rows.Count} stocks updated.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StockMarket] Tick error: {ex.Message}");
        }
    }

    private void ResetStockDayRange()
    {
        try
        {
            _sp.UpdateCreate(Constants.discordBotConnStr, "ResetStockDayRange", []);
            Console.WriteLine($"[StockMarket] 24h high/low reset at {DateTime.UtcNow:yyyy-MM-dd}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StockMarket] Day reset error: {ex.Message}");
        }
    }


    private async Task<string?> TrySaveSocialImageAsync(string url, string keyword)
    {
        if (!IsSupportedSocialUrl(url)) return null;

        try
        {
            using var http = httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; BigBirdBot/1.0)");

            using var head = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            string? contentType = head.Content.Headers.ContentType?.MediaType;

            if (IsImageContentType(contentType))
                return await DownloadSocialImageAsync(http, url, keyword, ExtFromContentType(contentType!));

            string html = await http.GetStringAsync(url);
            string? mediaUrl = ExtractOgTag(html, "og:image")
                            ?? ExtractOgTag(html, "og:video");

            if (mediaUrl is null) return null;

            string ext = Path.GetExtension(new Uri(mediaUrl).AbsolutePath).TrimStart('.');
            if (!IsSupportedExtension(ext)) return null;

            return await DownloadSocialImageAsync(http, mediaUrl, keyword, ext);
        }
        catch (Exception ex)
        {
            await logger.DebugAsync($"[TrySaveSocialImageAsync] {url} — {ex.Message}");
            return null;
        }
    }

    private async Task<string?> DownloadSocialImageAsync(
        HttpClient http, string imageUrl, string keyword, string ext)
    {
        if (!IsSupportedExtension(ext)) return null;

        string folder = keyword.Replace("KeywordMulti.", "");
        string dir = $@"C:\Temp\DiscordBot\{folder}";
        string fullPath = Path.Combine(dir, $"social_{DateTime.Now:yyyyMMdd_HHmmssfffff}.{ext}");

        Directory.CreateDirectory(dir);

        var bytes = await http.GetByteArrayAsync(imageUrl);
        await File.WriteAllBytesAsync(fullPath, bytes);

        await logger.DebugAsync($"[SocialImage] Saved → {fullPath}");
        return fullPath;
    }


    private async Task<bool> IsLinkWorkingAsync(string url)
    {
        if (!url.Contains("fxtwitter") && !url.Contains("vxtwitter"))
            return true;

        try
        {
            using var http = httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var body = await http.GetStringAsync(url, cts.Token);
            return !body.Contains("post doesn't exist");
        }
        catch
        {
            return false;
        }
    }

    private async Task AddAttachmentsAsync(
        SocketMessage msg, string tablename, string connStr, string userId)
    {
        tablename = tablename.Replace("KeywordMulti.", "");

        foreach (var attachment in msg.Attachments)
        {
            string[] parts = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string uniqueName = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}";
            string path = $@"C:\Temp\DiscordBot\{tablename}\{uniqueName}.{parts[1]}";

            _sp.UpdateCreate(connStr, "AddChatKeyword",
            [
                new SqlParameter("@FilePath",  path),
                new SqlParameter("@TableName", tablename),
                new SqlParameter("@UserID",    userId)
            ]);

            using var http = httpClientFactory.CreateClient();
            var bytes = await http.GetByteArrayAsync(attachment.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);
        }
    }


    private async Task SendLogAsync(string message, Color color)
    {
        var channel = client.GetGuild(LogGuildId)?.GetTextChannel(LogChannelId);
        if (channel is null) return;
        await channel.SendMessageAsync(embed: _embed
            .BuildMessageEmbed("Log", message, "", "BigBirdBot", color).Build());
    }

    private async Task NotifyOwnerAsync(string message)
    {
        var owner = await client.GetUserAsync(OwnerId);
        await owner.SendMessageAsync(message);
    }

    private static EmbedBuilder MakeEmbed(string title, Color color, string description) =>
        new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .WithDescription(description)
            .WithCurrentTimestamp();


    private static bool IsSupportedSocialUrl(string url) =>
        url.Contains("dl.fxtwitter.com") || url.Contains("bskx.app");

    private static bool IsImageContentType(string? ct) =>
        ct is "image/png" or "image/gif" or "image/jpeg";

    private static string ExtFromContentType(string ct) => ct switch
    {
        "image/png" => "png",
        "image/gif" => "gif",
        "image/jpeg" => "jpg",
        _ => ""
    };

    private static bool IsSupportedExtension(string ext) =>
        ext.ToLowerInvariant() is "png" or "gif" or "jpeg" or "jpg";

    private static string? ExtractOgTag(string html, string property)
    {
        string marker = $"property=\"{property}\"";
        int idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        int searchEnd = Math.Min(idx + 300, html.Length);
        int cIdx = html.IndexOf("content=\"", idx, searchEnd - idx, StringComparison.OrdinalIgnoreCase);
        if (cIdx < 0) return null;

        int start = cIdx + "content=\"".Length;
        int end = html.IndexOf('"', start);
        if (end < 0) return null;

        string value = html[start..end];
        return Uri.IsWellFormedUriString(value, UriKind.Absolute) ? value : null;
    }
}