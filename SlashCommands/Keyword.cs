using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

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
public class Keyword(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;

    /// <summary>
    /// ChatKeywordMap.Keyword was a non-persisted COMPUTED column in SQL Server
    /// (verified live: "Keyword = (replace([AddKeyword],'add',''))", is_persisted=False —
    /// AddChatKeywordMap's @Keyword parameter was genuinely unused, the column was always
    /// auto-derived on read). pgloader copied the computed *values* at migration time into a
    /// plain, non-computed Postgres column, so Postgres won't keep it in sync automatically
    /// any more — every write that sets/changes AddKeyword must also set Keyword explicitly
    /// here, using the same derivation, or the two columns will drift apart.
    /// </summary>
    private static string DeriveKeywordFromAddKeyword(string addKeyword) => addKeyword.Replace("add", "");


    /// <summary>Registers a new chat keyword and creates its backing storage directory.</summary>
    [SlashCommand("add", "Add a keyword that maps to one or more bot actions.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleAddAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        try
        {
            keyword = keyword.Trim().ToLowerInvariant();
            long guildId = (long)Context.Guild.Id;
            string addKeyword = "add" + keyword;

            bool exists = await db.ChatKeywordMaps.AnyAsync(m => m.ServerId == guildId && EF.Functions.ILike(m.AddKeyword, addKeyword));
            if (!exists)
            {
                db.ChatKeywordMaps.Add(new ChatKeywordMap
                {
                    ServerId = guildId,
                    AddKeyword = addKeyword,
                    Keyword = DeriveKeywordFromAddKeyword(addKeyword),
                    CreatedBy = Username
                });
                await db.SaveChangesAsync();
            }

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


    /// <summary>Permanently deletes a keyword and every entry mapped to it.</summary>
    [SlashCommand("delete", "Permanently remove a keyword and all its mappings.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleDeleteAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        keyword = keyword.Trim();

        // FIX: the source proc (DeleteChatKeyword) declared "@Keyword int" but every caller
        // passes a keyword NAME (string) — verified empirically that this throws
        // "Error converting data type nvarchar to int" for any real keyword, meaning this
        // command has never worked. Implementing the evidently-intended behavior instead of
        // porting the bug forward: delete the keyword's map entry, all its file/URL entries,
        // and any pending scheduled deliveries for it (matching the proc's 3-table intent).
        string addKeyword = "add" + keyword;
        db.ChatKeywordMaps.RemoveRange(db.ChatKeywordMaps.Where(m => EF.Functions.ILike(m.AddKeyword, addKeyword)));
        db.ChatKeywords.RemoveRange(db.ChatKeywords.Where(c => EF.Functions.ILike(c.ChatKeyword1, keyword)));
        await db.SaveChangesAsync();
        // BUG FIX: UsersScheduledKeyword has no real primary key (HasKey(UserId, ChatKeyword) in
        // the EF model doesn't hold — duplicate rows are possible, see HandleAddAsync), so a
        // tracked RemoveRange/SaveChanges here can throw DbUpdateConcurrencyException. Run as its
        // own bulk delete instead, which doesn't go through key-based change tracking.
        await db.UsersScheduledKeywords.Where(u => EF.Functions.ILike(u.ChatKeyword, keyword)).ExecuteDeleteAsync();

        await FollowupAsync(embed: _embed.BuildMessageEmbed(
            "Keyword Deleted",
            $"Keyword **{keyword}** and all its mappings were removed.",
            "", Username, Color.Blue).Build(), ephemeral: true);
    }


    /// <summary>Renames a keyword in the database and moves its on-disk storage directory to match.</summary>
    [SlashCommand("rename", "Rename an existing keyword and its directory.")]
    [CommandContextType(InteractionContextType.Guild)]
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
            long guildId = (long)Context.Guild.Id;
            string newAddKeyword = "add" + newName;

            // Source did 2 separate UPDATEs (ChatKeywordMap.AddKeyword, ChatKeyword.ChatKeyword)
            // with no explicit transaction — staged together here and saved once instead.
            // ChatKeyword can have many rows per keyword name (one per uploaded entry), all
            // renamed together, same as the source's WHERE-matched bulk UPDATE.
            var mapRow = await db.ChatKeywordMaps.FirstOrDefaultAsync(m =>
                m.ServerId == guildId && EF.Functions.ILike(m.Keyword ?? "", oldName));
            if (mapRow is not null)
            {
                mapRow.AddKeyword = newAddKeyword;
                mapRow.Keyword = DeriveKeywordFromAddKeyword(newAddKeyword);
            }

            var entryRows = await db.ChatKeywords.Where(c => EF.Functions.ILike(c.ChatKeyword1, oldName)).ToListAsync();
            foreach (var entry in entryRows) entry.ChatKeyword1 = newName;

            await db.SaveChangesAsync();

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

    /// <summary>/keyword alias subcommands — extra trigger words that serve entries from an existing keyword.</summary>
    [Group("alias", "Manage keyword aliases.")]
    public class AliasCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();

        private string Username => Context.User.Username;


        /// <summary>Creates an alias trigger word that serves the same entries as an existing keyword.</summary>
        [SlashCommand("add", "Create a trigger word that serves entries from an existing keyword.")]
        [CommandContextType(InteractionContextType.Guild)]
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
                long guildId = (long)Context.Guild.Id;

                bool exists = await db.ChatKeywordAliases.AnyAsync(a =>
                    EF.Functions.ILike(a.Alias, alias) && a.ServerId == guildId);

                if (exists)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Alias", $"**{alias}** is already an alias in this server.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                db.ChatKeywordAliases.Add(new ChatKeywordAlias
                {
                    Alias = alias,
                    Keyword = keyword,
                    ServerId = guildId,
                    CreatedBy = Username
                });
                await db.SaveChangesAsync();

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


        /// <summary>Removes an alias trigger word.</summary>
        [SlashCommand("delete", "Remove a keyword alias.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleDeleteAsync(
            [MinLength(1), MaxLength(50),
             Summary("alias", "The alias to remove")] string alias)
        {
            await DeferAsync(ephemeral: true);

            alias = alias.Trim().ToLowerInvariant();
            long guildId = (long)Context.Guild.Id;

            db.ChatKeywordAliases.RemoveRange(db.ChatKeywordAliases.Where(a =>
                EF.Functions.ILike(a.Alias, alias) && a.ServerId == guildId));
            await db.SaveChangesAsync();

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Alias Removed",
                $"Alias **{alias}** has been removed.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }


        /// <summary>Lists every alias currently pointing at a keyword.</summary>
        [SlashCommand("list", "List all aliases pointing to a keyword.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleListAsync(
            [MinLength(1), MaxLength(50),
             Summary("keyword", "Keyword to list aliases for")] string keyword)
        {
            await DeferAsync(ephemeral: true);

            keyword = keyword.Trim().ToLowerInvariant();
            long guildId = (long)Context.Guild.Id;

            var aliases = await db.ChatKeywordAliases.AsNoTracking()
                .Where(a => EF.Functions.ILike(a.Keyword, keyword) && a.ServerId == guildId)
                .OrderBy(a => a.Alias)
                .ToListAsync();

            if (aliases.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Keyword Aliases",
                    $"No aliases found for **{keyword}**.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
                return;
            }

            string list = string.Join(", ", aliases.Select(a => $"`{a.Alias}`"));

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                $"Aliases — {keyword}",
                $"**{aliases.Count}** alias(es): {list}",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }


    /// <summary>Shows a keyword's total entry count, creator, and up to 5 most recent entries.</summary>
    [SlashCommand("info", "Show stats and recent entries for a keyword.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleInfoAsync(
        [MinLength(1), MaxLength(50)] string keyword)
    {
        await DeferAsync(ephemeral: true);

        keyword = keyword.Trim().ToLowerInvariant();

        int entryCount = await db.ChatKeywords.CountAsync(c => EF.Functions.ILike(c.ChatKeyword1, keyword));

        // Source had no @ServerID filter here either — matches ANY server's registration
        // of this keyword name, not just the calling guild's. Preserved as-is.
        var mapRow = await db.ChatKeywordMaps.AsNoTracking()
            .FirstOrDefaultAsync(m => EF.Functions.ILike(m.Keyword ?? "", keyword));

        if (entryCount == 0 && mapRow is null)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Keyword Info", $"No data found for keyword **{keyword}**.", Username).Build(),
                ephemeral: true);
            return;
        }

        string created = mapRow?.CreatedBy ?? "Unknown";

        var recentPaths = await db.ChatKeywords.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.ChatKeyword1, keyword))
            .OrderByDescending(c => c.Id)
            .Select(c => c.FilePath)
            .ToListAsync();

        string recentStr = recentPaths.Count > 0
            ? string.Join("\n", recentPaths.Take(5).Select(fp =>
                fp.StartsWith(@"C:\") ? $"📁 `{Path.GetFileName(fp)}`" : $"🔗 {fp}"))
            : "*No entries yet*";

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🗂️  Keyword Info — {keyword}", "", Color.Blue,
            footer: $"Requested by {Username}", footerIconUrl: Context.User.GetAvatarUrl(),
            fields: [("Total Entries", entryCount.ToString(), true),
                     ("Created By", created, true),
                     ("Recent Entries (up to 5)", recentStr, false)]).Build(), ephemeral: true);
    }


    /// <summary>Lists every keyword registered in this server, paginated 15 per message.</summary>
    [SlashCommand("list", "List all keywords registered in this server.")]
    [CommandContextType(InteractionContextType.Guild)]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    public async Task HandleListAsync()
    {
        await DeferAsync(ephemeral: true);

        long guildId = (long)Context.Guild.Id;
        var rows = await db.ChatKeywordMaps.AsNoTracking()
            .Where(m => m.ServerId == guildId)
            .ToListAsync();

        if (rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Keywords", "No keywords have been registered in this server yet.",
                "", Username, Color.Blue).Build(), ephemeral: true);
            return;
        }

        const int pageSize = 15;

        for (int page = 0; page < (int)Math.Ceiling(rows.Count / (double)pageSize); page++)
        {
            var builder = _embed.BuildSimpleEmbed(
                $"📋  Registered Keywords — Page {page + 1}", "", Color.Blue,
                footer: $"{rows.Count} keyword(s) total  •  Requested by {Username}",
                footerIconUrl: Context.User.GetAvatarUrl());

            foreach (var r in rows.Skip(page * pageSize).Take(pageSize))
                builder.AddField($"`-{r.AddKeyword}`", $"Keyword: **{r.Keyword}**  •  Created by: {r.CreatedBy}", inline: false);

            await FollowupAsync(embed: builder.Build(), ephemeral: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // /keyword attachment [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>/keyword attachment subcommands — bulk-upload files as keyword entries.</summary>
    [Group("attachment", "Bulk-upload attachments to one or more keywords.")]
    public class AttachmentCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private static readonly HttpClient _http = new();

        private string Username => Context.User.Username;

        /// <summary>
        /// Upload up to 10 files at once and assign them to one or more comma-separated keywords.
        /// The same file is copied into every keyword's directory and registered in the DB for each.
        /// </summary>
        [SlashCommand("add", "Attach up to 10 files to one or more keywords at once.")]
        [CommandContextType(InteractionContextType.Guild)]
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

                        // Source did SET @FilePath = REPLACE(@FilePath, '''', '') before insert.
                        // CreatedOn: source used GETDATE() (local server time, not UTC) and
                        // ChatKeyword has no DB-level default — DateTime.Now to match exactly.
                        db.ChatKeywords.Add(new ChatKeyword
                        {
                            ChatKeyword1 = keyword,
                            FilePath = destPath.Replace("'", ""),
                            Nsfw = false,
                            CreatedOn = DateTime.Now.ToUniversalTime()
                        });
                        await db.SaveChangesAsync();
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

    /// <summary>/keyword url subcommands — manage individual URL entries attached to a keyword.</summary>
    [Group("url", "Manage URLs attached to keywords.")]
    public class UrlCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();

        private string Username => Context.User.Username;


        /// <summary>Removes one specific URL entry from a keyword.</summary>
        [SlashCommand("delete", "Remove a specific URL from a keyword table.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleDeleteAsync(
            [MinLength(1)] string url,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            url = url.Trim();
            keyword = keyword.Trim();

            db.ChatKeywords.RemoveRange(db.ChatKeywords.Where(c =>
                EF.Functions.ILike(c.FilePath, url) && EF.Functions.ILike(c.ChatKeyword1, keyword)));
            await db.SaveChangesAsync();

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "URL Deleted",
                $"Removed `{url}` from the **{keyword}** table.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // /keyword schedule [subcommand]
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>/keyword schedule subcommands — configure recurring DM deliveries of a keyword's entries to a specific user (sent by BotHost.RunScheduledKeywordsAsync).</summary>
    [Group("schedule", "Manage scheduled keyword deliveries for users.")]
    public class ScheduleCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();

        private string Username => Context.User.Username;


        /// <summary>Schedules a recurring keyword delivery DM for a user.</summary>
        [SlashCommand("add", "Schedule a recurring keyword delivery for a user.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleAddAsync(
            SocketGuildUser user,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                string userId = user.Id.ToString();
                keyword = keyword.Trim();

                // Source (AddUsersScheduledKeyword) used GETDATE() — local server time, not
                // UTC, unlike the Journal subsystem's procs. Matched here deliberately: wall-clock
                // math is done in local (Eastern) time, then converted to UTC right before it
                // touches the timestamptz column (Npgsql rejects Kind=Local writes outright).
                // u.ScheduledDateTime comes back from Npgsql as Kind=Utc, so scheduledAt must be
                // UTC too for the collision check's Year/Month/Day/Hour/Minute comparison to be
                // comparing like-for-like.
                var now = DateTime.Now;
                var scheduledAt = now.AddMinutes(1).ToUniversalTime();
                bool collision = await db.UsersScheduledKeywords.AnyAsync(u =>
                    u.ScheduledDateTime.Year == scheduledAt.Year && u.ScheduledDateTime.Month == scheduledAt.Month &&
                    u.ScheduledDateTime.Day == scheduledAt.Day && u.ScheduledDateTime.Hour == scheduledAt.Hour &&
                    u.ScheduledDateTime.Minute == scheduledAt.Minute);
                if (collision) scheduledAt = now.AddMinutes(2).ToUniversalTime();

                db.UsersScheduledKeywords.Add(new UsersScheduledKeyword
                {
                    UserId = userId,
                    ChatKeyword = keyword,
                    ScheduledDateTime = scheduledAt
                });
                await db.SaveChangesAsync();

                // Source GROUPed BY (ChatKeyword, ScheduledDateTime) for this user — since
                // ChatKeyword is itself part of the grouping key, STRING_AGG(ChatKeyword)
                // within each group is always just that one keyword name; functionally
                // equivalent to a distinct (ChatKeyword, ScheduledDateTime) projection.
                // Preserved as-is, including sending one followup per row (existing UX,
                // not something introduced by this conversion).
                var rows = await db.UsersScheduledKeywords.AsNoTracking()
                    .Where(u => u.UserId == userId)
                    .Select(u => new { u.ChatKeyword, u.ScheduledDateTime })
                    .Distinct()
                    .ToListAsync();

                if (rows.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Schedule Error", "No schedule record was returned.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                foreach (var row in rows)
                {
                    await FollowupAsync(embed: _embed.BuildMessageEmbed(
                        "Scheduled Event Added",
                        $"**{user.DisplayName}** will start receiving **{keyword}** on " +
                        $"**{row.ScheduledDateTime.ToLocalTime():MM/dd/yyyy hh:mm tt} ET**.\n\n" +
                        $"Current scheduled keywords: *{row.ChatKeyword}*",
                        "", Username, Color.Blue).Build(), ephemeral: true);
                }
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Schedule Error", ex.Message, Username).Build(), ephemeral: true);
            }
        }


        /// <summary>Cancels a scheduled keyword delivery for a user.</summary>
        [SlashCommand("remove", "Remove a scheduled keyword delivery for a user.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleRemoveAsync(
            SocketGuildUser user,
            [MinLength(1), MaxLength(50)] string keyword)
        {
            await DeferAsync(ephemeral: true);

            string userId = user.Id.ToString();
            keyword = keyword.Trim();

            // BUG FIX: see HandleDeleteAsync — UsersScheduledKeyword has no real primary key,
            // so this needs to be a bulk delete rather than a tracked RemoveRange/SaveChanges.
            await db.UsersScheduledKeywords.Where(u =>
                u.UserId == userId && EF.Functions.ILike(u.ChatKeyword, keyword)).ExecuteDeleteAsync();

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Schedule Removed",
                $"**{user.DisplayName}** will no longer receive **{keyword}**.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }


        /// <summary>Lists every scheduled keyword delivery configured for a user, with delivery times.</summary>
        [SlashCommand("list", "List the scheduled keyword deliveries for a user.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task HandleListAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            string userId = user.Id.ToString();
            var rows = await db.UsersScheduledKeywords.AsNoTracking()
                .Where(u => u.UserId == userId)
                .ToListAsync();

            if (rows.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "User Schedule",
                    $"**{user.DisplayName}** has no scheduled keyword deliveries.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
                return;
            }

            var builder = _embed.BuildSimpleEmbed(
                $"📅  Schedule — {user.DisplayName}", "", Color.Blue,
                footer: $"Requested by {Username}", footerIconUrl: Context.User.GetAvatarUrl())
                .WithThumbnailUrl(user.GetDisplayAvatarUrl() ?? user.GetDefaultAvatarUrl());

            foreach (var row in rows)
            {
                // ScheduledDateTime comes back from Npgsql as Kind=Utc; convert to local
                // (Eastern) to reproduce the source GETDATE()-based wall-clock display.
                string time = row.ScheduledDateTime.ToLocalTime().ToString("MM/dd/yyyy hh:mm tt") + " ET";
                builder.AddField(row.ChatKeyword, time, inline: true);
            }

            await FollowupAsync(embed: builder.Build(), ephemeral: true);
        }


        /// <summary>Owner-only: manually requeues a user's scheduled keyword delivery, e.g. after a failed send.</summary>
        [SlashCommand("requeue", "Requeue a user's scheduled keyword event after a delivery failure.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireOwner]
        public async Task HandleRequeueAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            string userId = user.Id.ToString();
            var rows = await db.UsersScheduledKeywords.AsNoTracking().Where(u => u.UserId == userId).ToListAsync();

            if (rows.Count == 0)
            {
                await FollowupAsync(embed: _embed.BuildMessageEmbed(
                    "Event Requeued", "This user does not have any scheduled thirsts to be sent out.",
                    "", Username, Color.Blue).Build(), ephemeral: true);
                return;
            }

            // Source (the redeployed UpdateUsersScheduledKeywordRequeue) used GETDATE() —
            // local server time, not UTC. Wall-clock math stays local; convert to UTC right
            // before the timestamptz write (Npgsql rejects Kind=Local), keep the local value
            // for the user-facing message.
            var newTimeLocal = DateTime.Now.AddMinutes(1);
            var newTimeUtc = newTimeLocal.ToUniversalTime();
            // BUG FIX: same non-unique "key" issue as HandleAddAsync/HandleDeleteAsync — bulk
            // ExecuteUpdate instead of a tracked read/mutate/SaveChanges against UsersScheduledKeyword.
            await db.UsersScheduledKeywords.Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ScheduledDateTime, newTimeUtc));

            string keywords = string.Join(", ", rows.Select(r => r.ChatKeyword));
            string message = $"The user was added successfully and the following keywords ({keywords}) will be sent at {newTimeLocal:hh:mm tt}";

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Event Requeued", message,
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }
}
