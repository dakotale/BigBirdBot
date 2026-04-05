using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Keyword management — /keyword [subcommand]
/// Reduces 11 top-level commands to 1 group, freeing 10 command slots.
///
/// /keyword add | delete | rename | info | list
/// /keyword alias      add | delete | list
/// /keyword url        delete
/// /keyword schedule   add | remove | list | requeue
/// </summary>
[Group("keyword", "Keyword management commands.")]
public class Keyword : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username => Context.User.Username;


    [SlashCommand("add", "Add a keyword that maps to one or more bot actions.")]
    [EnabledInDm(false)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleAddAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            keyword = keyword.Trim().ToLowerInvariant();

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddChatKeywordMap",
            [
                new SqlParameter("@ServerID",   (long)Context.Guild.Id),
                new SqlParameter("@Keyword",    keyword),
                new SqlParameter("@AddKeyword", "add" + keyword),
                new SqlParameter("@CreatedBy",  Username)
            ]);

            Directory.CreateDirectory(
                Path.Combine(Constants.Constants.keywordDirectory, keyword));

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Keyword Added",
                $"Keyword **{keyword}** and its directory were created successfully.\n" +
                $"Use `-add{keyword} <url>` to populate it.",
                "", $"Command from: {Username}", Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Keyword Error", ex.Message, "", $"Command from: {Username}", Color.Red).Build(),
                ephemeral: true);
        }
    }


    [SlashCommand("delete", "Permanently remove a keyword and all its mappings.")]
    [EnabledInDm(false)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleDeleteAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteChatKeyword",
        [
            new SqlParameter("@Keyword", keyword.Trim())
        ]);

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Keyword Deleted",
            $"Keyword **{keyword}** and all its mappings were removed.",
            "", Username, Color.Blue).Build(), ephemeral: true);
    }


    [SlashCommand("rename", "Rename an existing keyword and its directory.")]
    [EnabledInDm(false)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleRenameAsync(
        [MinLength(1), MaxLength(50)] string oldName,
        [MinLength(1), MaxLength(50)] string newName)
    {
        await DeferAsync(ephemeral: true);

        oldName = oldName.Trim().ToLowerInvariant();
        newName = newName.Trim().ToLowerInvariant();

        try
        {
            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "RenameChatKeyword",
            [
                new SqlParameter("@OldKeyword", oldName),
                new SqlParameter("@NewKeyword", newName),
                new SqlParameter("@ServerID",   (long)Context.Guild.Id)
            ]);

            string oldDir = Path.Combine(Constants.Constants.keywordDirectory, oldName);
            string newDir = Path.Combine(Constants.Constants.keywordDirectory, newName);

            if (Directory.Exists(oldDir))
                Directory.Move(oldDir, newDir);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Keyword Renamed",
                $"**{oldName}** → **{newName}** updated in the database and on disk.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Rename Error", ex.Message, Username).Build(), ephemeral: true);
        }
    }


    // ══════════════════════════════════════════════════════════════════════════
    // /keyword alias [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    [Group("alias", "Manage keyword aliases.")]
    public class AliasCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper     _embed = new();
        private readonly StoredProcedure _sp    = new();

        private string Username => Context.User.Username;


        [SlashCommand("add", "Create a trigger word that serves entries from an existing keyword.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleAddAsync(
            [MinLength(1), MaxLength(50),
             Summary("alias", "The new trigger word")] string alias,
            [MinLength(1), MaxLength(50),
             Summary("keyword", "Keyword whose entries will be served")] string keyword)
        {
            await DeferAsync(ephemeral: true);

            alias   = alias.Trim().ToLowerInvariant();
            keyword = keyword.Trim().ToLowerInvariant();

            try
            {
                var dt = _sp.Select(Constants.Constants.discordBotConnStr, "AddChatKeywordAlias",
                [
                    new SqlParameter("@Alias",     alias),
                    new SqlParameter("@Keyword",   keyword),
                    new SqlParameter("@ServerID",  (long)Context.Guild.Id),
                    new SqlParameter("@CreatedBy", Username)
                ]);

                if (dt.Rows.Count > 0 && dt.Rows[0]["Result"].ToString() == "exists")
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Alias", $"**{alias}** is already an alias in this server.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Alias Added",
                    $"**{alias}** → **{keyword}**: typing `{alias}` in chat will now trigger **{keyword}** entries.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Alias Error", ex.Message, Username).Build(), ephemeral: true);
            }
        }


        [SlashCommand("delete", "Remove a keyword alias.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleDeleteAsync(
            [MinLength(1), MaxLength(50),
             Summary("alias", "The alias to remove")] string alias)
        {
            await DeferAsync(ephemeral: true);

            alias = alias.Trim().ToLowerInvariant();

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteChatKeywordAlias",
            [
                new SqlParameter("@Alias",    alias),
                new SqlParameter("@ServerID", (long)Context.Guild.Id)
            ]);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Alias Removed",
                $"Alias **{alias}** has been removed.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }


        [SlashCommand("list", "List all aliases pointing to a keyword.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleListAsync(
            [MinLength(1), MaxLength(50),
             Summary("keyword", "Keyword to list aliases for")] string keyword)
        {
            await DeferAsync(ephemeral: true);

            keyword = keyword.Trim().ToLowerInvariant();

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetChatKeywordAliases",
            [
                new SqlParameter("@Keyword",  keyword),
                new SqlParameter("@ServerID", (long)Context.Guild.Id)
            ]);

            if (dt.Rows.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Keyword Aliases",
                    $"No aliases found for **{keyword}**.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
                return;
            }

            string list = string.Join(", ", dt.AsEnumerable().Select(r => $"`{r["Alias"]}`"));

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                $"Aliases — {keyword}",
                $"**{dt.Rows.Count}** alias(es): {list}",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }


    [SlashCommand("info", "Show stats and recent entries for a keyword.")]
    [EnabledInDm(false)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleInfoAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        keyword = keyword.Trim().ToLowerInvariant();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetChatKeywordInfo",
            [new SqlParameter("@Keyword", keyword)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Keyword Info", $"No data found for keyword **{keyword}**.", Username).Build(),
                ephemeral: true);
            return;
        }

        var row = dt.Rows[0];
        string count = row["EntryCount"].ToString()!;
        string created = row["CreatedBy"]?.ToString() ?? "Unknown";

        var recent = _sp.Select(Constants.Constants.discordBotConnStr, "GetChatKeywordRecent",
            [new SqlParameter("@Keyword", keyword)]);

        string recentStr = recent.AsEnumerable().Any()
            ? string.Join("\n", recent.AsEnumerable().Take(5).Select(r =>
            {
                string fp = r["FilePath"].ToString()!;
                return fp.StartsWith(@"C:\") ? $"📁 `{Path.GetFileName(fp)}`" : $"🔗 {fp}";
            }))
            : "*No entries yet*";

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🗂️  Keyword Info — {keyword}")
            .WithColor(Color.Blue)
            .AddField("Total Entries", count, inline: true)
            .AddField("Created By", created, inline: true)
            .AddField("Recent Entries (up to 5)", recentStr, inline: false)
            .WithFooter($"Requested by {Username}", Context.User.GetAvatarUrl())
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);
    }


    [SlashCommand("list", "List all keywords registered in this server.")]
    [EnabledInDm(false)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleListAsync()
    {
        await DeferAsync(ephemeral: true);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetChatKeywordsByServer",
            [new SqlParameter("@ServerID", (long)Context.Guild.Id)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Keywords", "No keywords have been registered in this server yet.",
                "", Username, Color.Blue).Build(), ephemeral: true);
            return;
        }

        const int pageSize = 15;
        var rows = dt.AsEnumerable().ToList();

        for (int page = 0; page < (int)Math.Ceiling(rows.Count / (double)pageSize); page++)
        {
            var builder = new EmbedBuilder()
                .WithTitle($"📋  Registered Keywords — Page {page + 1}")
                .WithColor(Color.Blue)
                .WithFooter($"{rows.Count} keyword(s) total  •  Requested by {Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp();

            foreach (var r in rows.Skip(page * pageSize).Take(pageSize))
            {
                string kw = r["Keyword"].ToString()!;
                string trigger = r["AddKeyword"].ToString()!;
                string creator = r["CreatedBy"].ToString()!;
                builder.AddField($"`-{trigger}`", $"Keyword: **{kw}**  •  Created by: {creator}", inline: false);
            }

            await FollowupAsync(embed: builder.Build(), ephemeral: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // /keyword attachment [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    [Group("attachment", "Bulk-upload attachments to one or more keywords.")]
    public class AttachmentCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper     _embed = new();
        private readonly StoredProcedure _sp    = new();
        private static readonly HttpClient _http = new();

        private string Username => Context.User.Username;

        /// <summary>
        /// Upload up to 10 files at once and assign them to one or more comma-separated keywords.
        /// The same file is copied into every keyword's directory and registered in the DB for each.
        /// </summary>
        [SlashCommand("add", "Attach up to 10 files to one or more keywords at once.")]
        [EnabledInDm(false)]
        public async Task HandleBulkAddAsync(
            [Summary("keywords", "Comma-separated keyword names, e.g. cat,dog,bird")] string keywords,
            IAttachment  file1,
            IAttachment? file2  = null,
            IAttachment? file3  = null,
            IAttachment? file4  = null,
            IAttachment? file5  = null,
            IAttachment? file6  = null,
            IAttachment? file7  = null,
            IAttachment? file8  = null,
            IAttachment? file9  = null,
            IAttachment? file10 = null)
        {
            await DeferAsync(ephemeral: true);

            // Collect non-null attachments
            var attachments = new[] { file1, file2, file3, file4, file5, file6, file7, file8, file9, file10 }
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList();

            // Parse and validate keyword list
            var keywordList = keywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (keywordList.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Bulk Attachment", "No valid keywords were provided.", Username).Build(),
                    ephemeral: true);
                return;
            }

            // Ensure each keyword directory exists
            var missingKeywords = keywordList
                .Where(k => !Directory.Exists(Path.Combine(Constants.Constants.keywordDirectory, k)))
                .ToList();

            if (missingKeywords.Count > 0)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Bulk Attachment",
                    $"The following keywords don't exist — create them with `/keyword add` first:\n" +
                    string.Join(", ", missingKeywords.Select(k => $"**{k}**")),
                    Username).Build(), ephemeral: true);
                return;
            }

            int saved   = 0;
            int skipped = 0;
            var errors  = new List<string>();

            foreach (var attachment in attachments)
            {
                // Sanitise filename and give it a unique prefix to avoid collisions
                string safeName   = Path.GetFileName(attachment.Filename)
                                        .Replace(" ", "_")
                                        .Replace("'", "");
                string uniqueName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{safeName}";

                // Download the file once to a temp location
                byte[] fileBytes;
                try
                {
                    fileBytes = await _http.GetByteArrayAsync(attachment.Url);
                }
                catch (Exception ex)
                {
                    errors.Add($"`{attachment.Filename}` — download failed: {ex.Message}");
                    skipped++;
                    continue;
                }

                // Copy into every keyword directory and register in DB
                foreach (string keyword in keywordList)
                {
                    string destDir  = Path.Combine(Constants.Constants.keywordDirectory, keyword);
                    string destPath = Path.Combine(destDir, uniqueName);

                    try
                    {
                        await File.WriteAllBytesAsync(destPath, fileBytes);

                        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddChatKeyword",
                        [
                            new SqlParameter("@FilePath",  destPath),
                            new SqlParameter("@TableName", keyword),
                            new SqlParameter("@UserID",    Context.User.Id.ToString())
                        ]);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"`{attachment.Filename}` → **{keyword}**: {ex.Message}");
                    }
                }

                saved++;
            }

            // Build result embed
            var desc = new System.Text.StringBuilder();
            desc.AppendLine($"**{saved}** file(s) added to **{keywordList.Count}** keyword(s): " +
                            string.Join(", ", keywordList.Select(k => $"`{k}`")));

            if (skipped > 0)
                desc.AppendLine($"\n⚠️ **{skipped}** file(s) skipped due to download errors.");

            if (errors.Count > 0)
            {
                desc.AppendLine("\n**Errors:**");
                foreach (var e in errors.Take(10))
                    desc.AppendLine($"• {e}");
            }

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "📎  Bulk Attachment Complete", desc.ToString(),
                "", Username, saved > 0 ? Color.Blue : Color.Red).Build(),
                ephemeral: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // /keyword url [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    [Group("url", "Manage URLs attached to keywords.")]
    public class UrlCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private readonly StoredProcedure _sp = new();

        private string Username => Context.User.Username;


        [SlashCommand("delete", "Remove a specific URL from a keyword table.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleDeleteAsync(
            [MinLength(1)] string url,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            url = url.Trim();
            keyword = keyword.Trim();

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteChatKeywordURL",
            [
                new SqlParameter("@FilePath", url),
                new SqlParameter("@Keyword",  keyword)
            ]);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "URL Deleted",
                $"Removed `{url}` from the **{keyword}** table.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // /keyword schedule [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    [Group("schedule", "Manage scheduled keyword deliveries for users.")]
    public class ScheduleCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private readonly StoredProcedure _sp = new();

        private string Username => Context.User.Username;


        [SlashCommand("add", "Schedule a recurring keyword delivery for a user.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleAddAsync(
            SocketGuildUser user,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                var dt = _sp.Select(Constants.Constants.discordBotConnStr, "AddUsersScheduledKeyword",
                [
                    new SqlParameter("@UserID",  (long)user.Id),
                    new SqlParameter("@Keyword", keyword.Trim())
                ]);

                if (dt.Rows.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Schedule Error", "No schedule record was returned.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                foreach (DataRow dr in dt.Rows)
                {
                    var scheduleTime = DateTime.Parse(dr["ScheduleTime"].ToString()!);
                    string tableList = dr["ScheduledEventTable"].ToString()!;

                    await FollowupAsync(embed: _embed.BuildMessageEmbed(
                        "Scheduled Event Added",
                        $"**{user.DisplayName}** will start receiving **{keyword}** on " +
                        $"**{scheduleTime:MM/dd/yyyy hh:mm tt} ET**.\n\n" +
                        $"Current scheduled keywords: *{tableList}*",
                        "", Username, Color.Blue).Build(), ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Schedule Error", ex.Message, Username).Build(), ephemeral: true);
            }
        }


        [SlashCommand("remove", "Remove a scheduled keyword delivery for a user.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleRemoveAsync(
            SocketGuildUser user,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteUsersScheduledKeyword",
            [
                new SqlParameter("@UserID",  user.Id.ToString()),
                new SqlParameter("@Keyword", keyword.Trim())
            ]);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Schedule Removed",
                $"**{user.DisplayName}** will no longer receive **{keyword}**.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }


        [SlashCommand("list", "List the scheduled keyword deliveries for a user.")]
        [EnabledInDm(false)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleListAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetUsersScheduledKeywords",
                [new SqlParameter("@UserID", user.Id.ToString())]);

            if (dt.Rows.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "User Schedule",
                    $"**{user.DisplayName}** has no scheduled keyword deliveries.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
                return;
            }

            var builder = new EmbedBuilder()
                .WithTitle($"📅  Schedule — {user.DisplayName}")
                .WithColor(Color.Blue)
                .WithThumbnailUrl(user.GetDisplayAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithFooter($"Requested by {Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp();

            foreach (DataRow row in dt.Rows)
            {
                string kw = row["ThirstTable"].ToString()!;
                string time = DateTime.Parse(row["ScheduleTime"].ToString()!)
                                   .ToString("MM/dd/yyyy hh:mm tt") + " ET";
                builder.AddField(kw, time, inline: true);
            }

            await FollowupAsync(embed: builder.Build(), ephemeral: true);
        }


        [SlashCommand("requeue", "Requeue a user's scheduled keyword event after a delivery failure.")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task HandleRequeueAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            var dt = _sp.Select(Constants.Constants.discordBotConnStr,
                "UpdateUsersScheduledKeywordRequeue",
                [new SqlParameter("@UserID", user.Id.ToString())]);

            foreach (DataRow dr in dt.Rows)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Event Requeued", dr["Message"].ToString()!,
                    "", Username, Color.Blue).Build(), ephemeral: true);
            }
        }
    }
}
