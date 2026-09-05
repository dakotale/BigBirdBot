using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
///
/// All keyword data access goes through <see cref="KeywordService"/> (EF Core).
/// </summary>
[Group("keyword", "Keyword management commands.")]
public class Keyword(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;


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

            await keywords.AddMapAsync(Context.Guild.Id, "add" + keyword, Username);

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

        await keywords.DeleteKeywordAsync(keyword.Trim());

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
            await keywords.RenameKeywordAsync(oldName, newName, Context.Guild.Id);

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


    /// <summary>/keyword alias subcommands — extra trigger words that serve entries from an existing keyword.</summary>
    [Group("alias", "Manage keyword aliases.")]
    public class AliasCommands(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
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
                bool added = await keywords.AddAliasAsync(alias, keyword, Context.Guild.Id, Username);

                if (!added)
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

            await keywords.DeleteAliasAsync(alias, Context.Guild.Id);

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

            var aliases = await keywords.GetAliasesAsync(keyword, Context.Guild.Id);

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

        var info = await keywords.GetInfoAsync(keyword);

        if (info is null)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Keyword Info", $"No data found for keyword **{keyword}**.", Username).Build(),
                ephemeral: true);
            return;
        }

        string count = info.EntryCount.ToString();
        string created = info.CreatedBy ?? "Unknown";

        var recent = await keywords.GetRecentEntriesAsync(keyword);

        string recentStr = recent.Count > 0
            ? string.Join("\n", recent.Take(5).Select(fp =>
                KeywordFiles.IsLocalFile(fp) ? $"📁 `{Path.GetFileName(KeywordFiles.Resolve(fp))}`" : $"🔗 {fp}"))
            : "*No entries yet*";

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"🗂️  Keyword Info — {keyword}", "", Color.Blue,
            footer: $"Requested by {Username}", footerIconUrl: Context.User.GetAvatarUrl(),
            fields: [("Total Entries", count, true),
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

        var rows = await keywords.GetKeywordsForServerAsync(Context.Guild.Id);

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
    public class AttachmentCommands(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper      _embed = new();
        private static readonly HttpClient _http = new();

        private string Username => Context.User.Username;

        /// <summary>
        /// Upload up to 10 files at once and assign them to one or more comma-separated keywords.
        /// The same file is copied into every keyword's directory and registered in the DB for each.
        /// </summary>
        [SlashCommand("add", "Attach up to 10 files to one or more keywords at once.")]
        [CommandContextType(InteractionContextType.Guild)]
        public async Task HandleBulkAddAsync(
            [Summary("keywords", "Comma-separated keyword names, e.g. cat,dog,bird")] string keywordNames,
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
            var keywordList = keywordNames
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
                    string destPath = Path.Combine(Constants.Constants.keywordDirectory, keyword, uniqueName);

                    try
                    {
                        await File.WriteAllBytesAsync(destPath, fileBytes);
                        await keywords.AddEntryAsync(keyword, KeywordFiles.ToStored(keyword, uniqueName));
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

    /// <summary>/keyword url subcommands — manage individual URL entries attached to a keyword.</summary>
    [Group("url", "Manage URLs attached to keywords.")]
    public class UrlCommands(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
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

            await keywords.DeleteEntryAsync(url, keyword);

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "URL Deleted",
                $"Removed `{url}` from the **{keyword}** table.",
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }

    /// <summary>/keyword schedule subcommands — configure recurring DM deliveries of a keyword's entries to a specific user (sent by BotHost.RunScheduledKeywordsAsync).</summary>
    [Group("schedule", "Manage scheduled keyword deliveries for users.")]
    public class ScheduleCommands(KeywordService keywords) : InteractionModuleBase<SocketInteractionContext>
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
                var summaries = await keywords.AddScheduleAsync(user.Id.ToString(), keyword.Trim());

                if (summaries.Count == 0)
                {
                    await FollowupAsync(embed: _embed.BuildErrorEmbed(
                        "Schedule Error", "No schedule record was returned.", Username).Build(),
                        ephemeral: true);
                    return;
                }

                foreach (var summary in summaries)
                {
                    await FollowupAsync(embed: _embed.BuildMessageEmbed(
                        "Scheduled Event Added",
                        $"**{user.DisplayName}** will start receiving **{keyword}** on " +
                        $"**{summary.ScheduleTime:MM/dd/yyyy hh:mm tt} ET**.\n\n" +
                        $"Current scheduled keywords: *{summary.KeywordsCsv}*",
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

            await keywords.RemoveScheduleAsync(user.Id.ToString(), keyword.Trim());

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

            var schedule = await keywords.GetUserScheduleAsync(user.Id.ToString());

            if (schedule.Count == 0)
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

            foreach (var row in schedule)
                builder.AddField(row.Keyword, row.ScheduleTime.ToString("MM/dd/yyyy hh:mm tt") + " ET", inline: true);

            await FollowupAsync(embed: builder.Build(), ephemeral: true);
        }


        /// <summary>Owner-only: manually requeues a user's scheduled keyword delivery, e.g. after a failed send.</summary>
        [SlashCommand("requeue", "Requeue a user's scheduled keyword event after a delivery failure.")]
        [CommandContextType(InteractionContextType.Guild)]
        [RequireOwner]
        public async Task HandleRequeueAsync(SocketGuildUser user)
        {
            await DeferAsync(ephemeral: true);

            string message = await keywords.RequeueScheduleAsync(user.Id.ToString());

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "Event Requeued", message,
                "", Username, Color.Blue).Build(), ephemeral: true);
        }
    }
}
