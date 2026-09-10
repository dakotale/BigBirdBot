using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// <c>/timezone</c> — a member's saved time zone, used by <c>/remind</c> so they don't re-enter
/// a UTC offset every time. One value per user, shared across every server. Accepts an offset
/// (<c>-5</c>, <c>+5:30</c>), an IANA name (<c>Europe/London</c>), or <c>clear</c>; omit the
/// argument to view the current setting.
/// </summary>
public class TimezoneCommands(SchedulingService scheduling) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private string Username => Context.User.Username;

    private static readonly string[] ClearWords = ["clear", "none", "off", "unset", "reset"];

    /// <summary>Shows, sets, or clears the caller's saved time zone.</summary>
    [SlashCommand("timezone", "Show, set, or clear your saved time zone (used by /remind).")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleTimezoneAsync(
        [Summary("zone", "An offset (-5, +5:30), an IANA name (Europe/London), or 'clear'. Omit to view.")]
        string? zone = null)
    {
        await DeferAsync(ephemeral: true);

        string userId = Context.User.Id.ToString();

        // ── View ────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(zone))
        {
            string? saved = await scheduling.GetUserTimeZoneAsync(userId);
            await Reply("🕒  Time Zone",
                saved is null
                    ? "You haven't set a time zone. Run `/timezone -5` or `/timezone Europe/London`."
                    : $"Your time zone is **{saved}**.  Local time now: **{LocalNow(saved)}**.",
                saved is null ? EmbedColors.Grey : EmbedColors.Blue);
            return;
        }

        // ── Clear ───────────────────────────────────────────────────────────
        if (ClearWords.Contains(zone.Trim().ToLowerInvariant()))
        {
            bool had = await scheduling.ClearUserTimeZoneAsync(userId);
            await Reply("🕒  Time Zone",
                had ? "Your saved time zone was cleared." : "You didn't have a time zone saved.",
                EmbedColors.Grey);
            return;
        }

        // ── Set ─────────────────────────────────────────────────────────────
        if (!TimeZoneResolver.TryResolve(zone, out _, out string canonical))
        {
            await Reply("🕒  Unknown Time Zone",
                $"Couldn't understand `{zone}`. Try an offset like `-5` / `+5:30`, or an IANA name like `America/New_York`.",
                EmbedColors.Red);
            return;
        }

        await scheduling.SetUserTimeZoneAsync(userId, canonical);
        await Reply("🕒  Time Zone Saved",
            $"Saved as **{canonical}**.  Local time now: **{LocalNow(canonical)}**.\n`/remind` will use this from now on.",
            EmbedColors.Green);
    }

    private static string LocalNow(string token) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneResolver.Resolve(token))
            .ToString("MMM d, h:mm tt");

    private Task Reply(string title, string body, Color color) =>
        FollowupAsync(embed: _embed.BuildMessageEmbed(title, body, "", Username, color).Build(), ephemeral: true);
}
