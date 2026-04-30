using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands
{
    public class QuoteCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private readonly StoredProcedure _sp = new();
        private readonly IHttpClientFactory _httpFactory;

        private static readonly ConcurrentDictionary<string, (List<Embed> Pages, int CurrentPage, DateTime ExpiresAt)>
            _sessions = new();

        private const int PageSize = 5;

        private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

        public QuoteCommands(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        // ── Context Menu ──────────────────────────────────────────────────────────

        [MessageCommand("Save Quote")]
        [CommandContextType(InteractionContextType.Guild)]
        public async Task HandleSaveQuoteAsync(IMessage message)
        {
            await DeferAsync(ephemeral: true);

            var configDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetGuildQuoteConfig",
            [
                new SqlParameter("@GuildId", (long)Context.Guild.Id)
            ]);

            if (configDt.Rows.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Save Quote",
                    "No quote archive channel is configured. Use `/quote setup` first.",
                    Context.User.Username).Build(), ephemeral: true);
                return;
            }

            ulong archiveChannelId = (ulong)(long)configDt.Rows[0]["ArchiveChannelId"];

            string content = string.IsNullOrWhiteSpace(message.Content)
                ? (message.Attachments.Count > 0 ? "[attachment]" : "[empty message]")
                : message.Content;

            string messageUrl = $"https://discord.com/channels/{Context.Guild.Id}/{message.Channel.Id}/{message.Id}";

            // Check for attachment to re-upload
            string? attachmentUrl = null;
            if (message.Attachments.Count > 0)
            {
                var attachment = message.Attachments.First();
                try
                {
                    var httpClient = _httpFactory.CreateClient();
                    var bytes = await httpClient.GetByteArrayAsync(attachment.Url);
                    string ext = Path.GetExtension(attachment.Filename);
                    string filename = $"quote_{message.Id}{ext}";
                    var archiveChannel = Context.Guild.GetTextChannel(archiveChannelId);
                    if (archiveChannel is not null)
                    {
                        using var ms = new MemoryStream(bytes);
                        var msg = await archiveChannel.SendFileAsync(ms, filename);
                        attachmentUrl = msg.Attachments.FirstOrDefault()?.Url;
                    }
                }
                catch { /* attachment re-upload failed — continue without it */ }
            }

            var insertDt = _sp.Select(Constants.Constants.discordBotConnStr, "InsertQuote",
            [
                new SqlParameter("@GuildId",            (long)Context.Guild.Id),
                new SqlParameter("@AuthorId",           (long)message.Author.Id),
                new SqlParameter("@AuthorUsername",     message.Author.Username),
                new SqlParameter("@SavedByUserId",      (long)Context.User.Id),
                new SqlParameter("@SavedByUsername",    Context.User.Username),
                new SqlParameter("@Content",            content),
                new SqlParameter("@OriginalMessageUrl", messageUrl),
                new SqlParameter("@ArchiveMessageUrl",  (object?)null ?? DBNull.Value),
                new SqlParameter("@AttachmentUrl",      (object?)attachmentUrl ?? DBNull.Value)
            ]);

            bool duplicate = insertDt.Rows.Count > 0 && (int)insertDt.Rows[0]["Duplicate"] == 1;

            if (duplicate)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Save Quote", "This message has already been saved as a quote.", Context.User.Username).Build(),
                    ephemeral: true);
                return;
            }

            int quoteId = (int)insertDt.Rows[0]["QuoteId"];

            // Post quote embed to archive channel
            var quoteEmbed = BuildQuoteEmbed(
                quoteId,
                message.Author.Username,
                message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl(),
                content,
                messageUrl,
                attachmentUrl,
                Context.User.Username,
                DateTime.UtcNow);

            var jumpButton = new ComponentBuilder()
                .WithButton("Jump to Original", style: ButtonStyle.Link, url: messageUrl)
                .Build();

            var archiveTextChannel = Context.Guild.GetTextChannel(archiveChannelId);
            if (archiveTextChannel is not null)
            {
                var archiveMsg = await archiveTextChannel.SendMessageAsync(
                    embed: quoteEmbed, components: jumpButton);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpdateQuoteArchiveUrl",
                [
                    new SqlParameter("@QuoteId",          quoteId),
                    new SqlParameter("@ArchiveMessageUrl", archiveMsg.GetJumpUrl())
                ]);
            }

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Quote Saved",
                $"Quote **#{quoteId}** has been saved to the archive.",
                "", Context.User.Username, EmbedColors.Green).Build(), ephemeral: true);
        }

        // ── Slash Commands ────────────────────────────────────────────────────────

        [Group("quote", "Browse and manage saved quotes.")]
        [CommandContextType(InteractionContextType.Guild)]
        public class QuoteSubCommands : InteractionModuleBase<SocketInteractionContext>
        {
            private readonly EmbedHelper _embed = new();
            private readonly StoredProcedure _sp = new();

            private string Username => Context.User.Username;

            [SlashCommand("setup", "Set the channel where quotes are archived.")]
            [RequireUserPermission(GuildPermission.ManageChannels)]
            public async Task HandleSetupAsync(
                [Summary("channel", "The text channel to use as the quote archive.")] ITextChannel channel)
            {
                await DeferAsync(ephemeral: true);

                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpsertGuildQuoteConfig",
                [
                    new SqlParameter("@GuildId",          (long)Context.Guild.Id),
                    new SqlParameter("@ArchiveChannelId", (long)channel.Id)
                ]);

                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Quote Setup",
                    $"Quotes will be archived in {channel.Mention}.",
                    "", Username, EmbedColors.Green).Build(), ephemeral: true);
            }

            [SlashCommand("random", "Show a random saved quote.")]
            public async Task HandleRandomAsync()
            {
                await DeferAsync();

                var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetRandomQuote",
                [
                    new SqlParameter("@GuildId", (long)Context.Guild.Id)
                ]);

                if (dt.Rows.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Random Quote", "No quotes have been saved yet.", Username).Build(), ephemeral: true);
                    return;
                }

                var embed = RowToQuoteEmbed(dt.Rows[0]);
                string? archiveUrl = dt.Rows[0]["ArchiveMessageUrl"] as string;
                string originalUrl = dt.Rows[0]["OriginalMessageUrl"].ToString()!;

                var buttons = new ComponentBuilder()
                    .WithButton("Jump to Original", style: ButtonStyle.Link, url: originalUrl);
                if (!string.IsNullOrEmpty(archiveUrl))
                    buttons.WithButton("View in Archive", style: ButtonStyle.Link, url: archiveUrl);

                await FollowupAsync(embed: embed, components: buttons.Build());
            }

            [SlashCommand("search", "Search saved quotes by text.")]
            public async Task HandleSearchAsync(
                [Summary("query", "Text to search for in quote content.")] string query)
            {
                await DeferAsync(ephemeral: true);

                var dt = _sp.Select(Constants.Constants.discordBotConnStr, "SearchQuotes",
                [
                    new SqlParameter("@GuildId", (long)Context.Guild.Id),
                    new SqlParameter("@Query",   query)
                ]);

                if (dt.Rows.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Quote Search", $"No quotes found matching \"{query}\".", Username).Build(),
                        ephemeral: true);
                    return;
                }

                var pages = BuildPages(dt, $"Quote Search — \"{query}\"");
                await SendPaginatedAsync(pages);
            }

            [SlashCommand("user", "Browse all quotes from a specific user.")]
            public async Task HandleUserAsync(
                [Summary("user", "The user whose quotes to browse.")] IUser user)
            {
                await DeferAsync(ephemeral: true);

                var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetQuotesByUser",
                [
                    new SqlParameter("@GuildId",  (long)Context.Guild.Id),
                    new SqlParameter("@AuthorId", (long)user.Id)
                ]);

                if (dt.Rows.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "User Quotes", $"No quotes found for **{user.Username}**.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                var pages = BuildPages(dt, $"Quotes by {user.Username}");
                await SendPaginatedAsync(pages);
            }

            private async Task SendPaginatedAsync(List<Embed> pages)
            {
                string sessionKey = Context.User.Id.ToString();

                _sessions[sessionKey] = (pages, 0, DateTime.UtcNow.AddMinutes(15));

                var components = BuildNavComponents(0, pages.Count, sessionKey);

                await FollowupAsync(embed: pages[0], components: components, ephemeral: true);
            }
        }

        // ── Pagination button handler ─────────────────────────────────────────────

        [ComponentInteraction("quote:nav:p:*")]
        public async Task HandlePrevAsync(string userId)
        {
            await HandleNavAsync(userId, -1);
        }

        [ComponentInteraction("quote:nav:n:*")]
        public async Task HandleNextAsync(string userId)
        {
            await HandleNavAsync(userId, +1);
        }

        private async Task HandleNavAsync(string userId, int delta)
        {
            if (!_sessions.TryGetValue(userId, out var session) || session.ExpiresAt < DateTime.UtcNow)
            {
                await RespondAsync("This session has expired. Run the command again.", ephemeral: true);
                return;
            }

            int newPage = Math.Clamp(session.CurrentPage + delta, 0, session.Pages.Count - 1);
            _sessions[userId] = (session.Pages, newPage, session.ExpiresAt);

            var components = BuildNavComponents(newPage, session.Pages.Count, userId);
            await (Context.Interaction as SocketMessageComponent)!
                .UpdateAsync(p =>
                {
                    p.Embed = session.Pages[newPage];
                    p.Components = components;
                });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static List<Embed> BuildPages(DataTable dt, string title)
        {
            var pages = new List<Embed>();
            int total = dt.Rows.Count;
            int totalPages = (int)Math.Ceiling(total / (double)PageSize);

            for (int p = 0; p < totalPages; p++)
            {
                var builder = new EmbedBuilder()
                    .WithTitle(title)
                    .WithColor(EmbedColors.Blue)
                    .WithFooter($"Page {p + 1} of {totalPages} • {total} quote(s)");

                int start = p * PageSize;
                int end   = Math.Min(start + PageSize, total);

                for (int i = start; i < end; i++)
                {
                    var row = dt.Rows[i];
                    int quoteId = (int)row["QuoteId"];
                    string author = row["AuthorUsername"].ToString()!;
                    string snippet = row["Content"].ToString()!;
                    if (snippet.Length > 200) snippet = snippet[..200] + "…";
                    string savedAt = ((DateTime)row["SavedAt"]).ToString("yyyy-MM-dd");

                    builder.AddField($"#{quoteId} — {author} ({savedAt})", snippet);
                }

                pages.Add(builder.Build());
            }

            return pages;
        }

        private static MessageComponent BuildNavComponents(int currentPage, int totalPages, string userId)
        {
            return new ComponentBuilder()
                .WithButton("◀ Prev", $"quote:nav:p:{userId}",
                    ButtonStyle.Secondary, disabled: currentPage == 0)
                .WithButton("Next ▶", $"quote:nav:n:{userId}",
                    ButtonStyle.Secondary, disabled: currentPage == totalPages - 1)
                .Build();
        }

        private static Embed RowToQuoteEmbed(DataRow row)
        {
            int quoteId         = (int)row["QuoteId"];
            string author       = row["AuthorUsername"].ToString()!;
            string content      = row["Content"].ToString()!;
            string originalUrl  = row["OriginalMessageUrl"].ToString()!;
            string? attachUrl   = row["AttachmentUrl"] as string;
            string savedBy      = row["SavedByUsername"].ToString()!;
            var savedAt         = (DateTime)row["SavedAt"];

            return BuildQuoteEmbed(quoteId, author, null, content, originalUrl, attachUrl, savedBy, savedAt);
        }

        private static Embed BuildQuoteEmbed(
            int quoteId,
            string authorUsername,
            string? authorAvatarUrl,
            string content,
            string originalUrl,
            string? attachmentUrl,
            string savedByUsername,
            DateTime savedAt)
        {
            var builder = new EmbedBuilder()
                .WithColor(EmbedColors.Gold)
                .WithDescription($"\"{content}\"")
                .WithFooter($"Quote #{quoteId} • Saved by {savedByUsername} • {savedAt:yyyy-MM-dd}");

            if (authorAvatarUrl is not null)
                builder.WithAuthor(authorUsername, authorAvatarUrl);
            else
                builder.WithAuthor(authorUsername);

            if (!string.IsNullOrEmpty(attachmentUrl))
            {
                string ext = Path.GetExtension(new Uri(attachmentUrl).LocalPath).ToLowerInvariant();
                if (ImageExtensions.Contains(ext))
                    builder.WithImageUrl(attachmentUrl);
            }

            return builder.Build();
        }
    }
}
