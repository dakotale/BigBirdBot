using Discord;
using Discord.Interactions;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// <c>/help</c> — a browsable command reference. Discord has no API to enumerate another bot's
/// help text, so the content is maintained here by hand; keep it in sync with the README when
/// commands change.
/// </summary>
public class HelpCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private static readonly (string Key, string Title, string Body)[] Categories =
    [
        ("music", "🎵  Music",
            "`/join` `/leave` `/play <query>` `/playnext <query>` `/forceskip` `/pause` `/resume` `/stop`\n" +
            "`/nowplaying` `/queue` `/volume <0–100>` `/loop <off|track|queue>` `/shuffle` `/clear`\n" +
            "`/remove <pos>` `/swap <a> <b>` `/seek <hh:mm:ss>`"),

        ("keywords", "💬  Keywords",
            "`/keyword add|delete|rename|info|list` · `/keyword alias add|delete|list`\n" +
            "`/keyword attachment add` · `/keyword url delete` · `/keyword schedule add|remove|list`\n" +
            "Trigger a keyword by typing it in chat. Add entries with `-<keyword> <url/text>` or an attachment. " +
            "Requires **Manage Messages**."),

        ("ai", "🤖  AI",
            "`/chat <message> <new-conversation> [personality]` — talk to a character persona\n" +
            "`/support <message> <new-conversation> <topic>` — a mental-health / identity support guide\n" +
            "`/detectai <image>` — check if an image is AI-generated\n" +
            "`/mood <mood>` — a Spotify track that matches a vibe"),

        ("utility", "🧰  Utility",
            "`/random <max>` · `/poll <question> <answers…>` · `/dnddice <n> <sides> [mod]` · `/colorpreview <hex>`\n" +
            "`/timezone [zone]` · `/remind <msg> <when> [tz]` · `/reminders` · `/reminddelete <id>`\n" +
            "`/fixembed` *(Manage Server)* — toggle Twitter/Reddit/TikTok/Bluesky embed fixing"),

        ("server", "🏰  Server",
            "`/avatar [user]` · `/serverinfo` · `/setrolecolor <hex> [member]` · `/reportbug <text>`\n" +
            "`/polldnd <user>` — poll the next 7 days for a D&D session\n" +
            "`/addbirthday <user> <month> <day>` · `/birthdays` · `/birthdayremove <user>`"),

        ("moderation", "🔨  Moderation & Admin",
            "`/mod kick|ban|unban|timeout|untimeout|slowmode` · `/role add|remove`\n" +
            "`/purge <count>` *(Manage Messages)* · `/pronoun` *(Manage Messages)*\n" +
            "`/botnick <name>` *(Manage Roles)* · `/announcements` *(Manage Guild)*\n" +
            "`/autorole set|clear|status` *(Manage Roles)*\n" +
            "Each `/mod` action needs the matching permission from both you and the bot."),
    ];

    /// <summary>Shows all command categories, or the commands in one chosen category.</summary>
    [SlashCommand("help", "Browse the bot's commands.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleHelpAsync(
        [Summary("category", "Which group of commands to show"),
         Choice("Music", "music"),
         Choice("Keywords", "keywords"),
         Choice("AI", "ai"),
         Choice("Utility", "utility"),
         Choice("Server", "server"),
         Choice("Moderation & Admin", "moderation")]
        string? category = null)
    {
        await DeferAsync(ephemeral: true);

        const string footer = "Only you can see this  •  /help category: for one section";

        if (category is null)
        {
            var overview = _embed.BuildSimpleEmbed(
                "📖  BigBirdBot — Commands",
                "Everything the bot can do, by category. Use `/help category:` to focus on one.",
                Color.Blue, footer: footer);

            foreach (var (_, title, body) in Categories)
                overview.AddField(title, body, inline: false);

            await FollowupAsync(embed: overview.Build(), ephemeral: true);
            return;
        }

        var (_, catTitle, catBody) = Categories.First(c => c.Key == category);
        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            catTitle, catBody, Color.Blue, footer: footer).Build(), ephemeral: true);
    }
}
