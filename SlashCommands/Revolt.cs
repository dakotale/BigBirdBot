using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Collections.Concurrent;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /revolt @target
///
/// Five users with under 5,000 credits must agree to revolt against a target.
/// Once five unique revolters have joined within 5 minutes, the target is
/// guillotined: their credits and liquidated stock portfolio are seized and
/// split equally across every user in the server.
/// </summary>
public class Revolt : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly Economy _eco = new();

    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";
    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly Color ColourRed = new(237, 66, 69);
    private static readonly Color ColourGold = new(255, 215, 0);
    private static readonly Color ColourGrey = new(128, 128, 128);

    // ── In-memory revolt state ────────────────────────────────────────────────
    // Key: "serverId:targetUserId"
    // Value: revolt state including revolters and expiry
    private record RevoltState(
        string TargetId,
        string TargetName,
        HashSet<string> Revolters,    // userIds who have joined
        DateTime ExpiresAt,
        ulong AnnouncementChannelId,
        ulong AnnouncementMessageId);

    private static readonly ConcurrentDictionary<string, RevoltState> _activeRevolts = new();

    private const int RequiredRevolters = 3;
    private const decimal MaxRevolterBalance = 50_000m;
    private static readonly TimeSpan RevoltWindow = TimeSpan.FromMinutes(5);

    // ── /revolt ───────────────────────────────────────────────────────────────

    [SlashCommand("revolt", "Rise up against a wealthy user — 3 paupers must agree within 5 minutes.")]
    [EnabledInDm(false)]
    public async Task HandleRevoltAsync(IUser target)
    {
        await DeferAsync();

        // ── Basic guards ───────────────────────────────────────────────────────
        if (target.Id == Context.User.Id)
        {
            await ErrorAsync("You cannot revolt against yourself.");
            return;
        }

        if (target.IsBot)
        {
            await ErrorAsync("Bots have no credits to seize.");
            return;
        }

        // ── Check revolter's own balance ───────────────────────────────────────
        decimal revolterBalance = _eco.GetBalance(UserId, ServerId);
        if (revolterBalance >= MaxRevolterBalance)
        {
            await ErrorAsync(
                $"Only the poor may revolt. Your balance is {CreditHelper.Format(revolterBalance)} — " +
                $"you need under {CreditHelper.Format(MaxRevolterBalance)} to join a revolt.");
            return;
        }

        // ── Check target actually has something worth seizing ──────────────────
        decimal targetBalance = _eco.GetBalance(target.Id.ToString(), ServerId);
        if (targetBalance <= 0m)
        {
            await ErrorAsync($"**{target.Username}** has nothing worth taking. Pick a wealthier target.");
            return;
        }

        string revoltKey = $"{ServerId}:{target.Id}";

        // ── Join existing revolt or start a new one ────────────────────────────
        _activeRevolts.AddOrUpdate(
            revoltKey,
            // Create new revolt
            _ =>
            {
                var state = new RevoltState(
                    TargetId: target.Id.ToString(),
                    TargetName: target.Username,
                    Revolters: new HashSet<string> { UserId },
                    ExpiresAt: DateTime.UtcNow.Add(RevoltWindow),
                    AnnouncementChannelId: 0,
                    AnnouncementMessageId: 0);
                return state;
            },
            // Join existing
            (_, existing) =>
            {
                if (DateTime.UtcNow > existing.ExpiresAt)
                {
                    // Expired — start fresh
                    return existing with
                    {
                        Revolters = new HashSet<string> { UserId },
                        ExpiresAt = DateTime.UtcNow.Add(RevoltWindow),
                        AnnouncementChannelId = 0,
                        AnnouncementMessageId = 0
                    };
                }
                existing.Revolters.Add(UserId);
                return existing;
            });

        var revolt = _activeRevolts[revoltKey];

        // ── Check if quorum reached ────────────────────────────────────────────
        if (revolt.Revolters.Count >= RequiredRevolters)
        {
            // Remove immediately to prevent race conditions
            _activeRevolts.TryRemove(revoltKey, out _);
            await ExecuteGuillotine(target, revolt);
            return;
        }

        // ── Not yet — show progress ────────────────────────────────────────────
        int needed = RequiredRevolters - revolt.Revolters.Count;
        long expiry = new DateTimeOffset(revolt.ExpiresAt).ToUnixTimeSeconds();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("⚔️  Revolt Started!")
            .WithColor(ColourRed)
            .WithDescription(
                $"**{revolt.Revolters.Count}/{RequiredRevolters}** revolters have joined against **{target.Username}**.\n\n" +
                $"**{needed}** more {(needed == 1 ? "person" : "people")} with under " +
                $"{CreditHelper.Format(MaxRevolterBalance)} must run `/revolt {target.Username}` to proceed.\n\n" +
                $"⏱ Expires <t:{expiry}:R>")
            .WithFooter($"{Username} joined the revolt", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }

    // ── Guillotine execution ──────────────────────────────────────────────────

    private async Task ExecuteGuillotine(IUser target, RevoltState revolt)
    {
        string targetId = target.Id.ToString();

        // ── Seize credits ──────────────────────────────────────────────────────
        decimal seizedCredits = _eco.GetBalance(targetId, ServerId);
        if (seizedCredits > 0m)
            _eco.DeductCredits(targetId, ServerId, seizedCredits, "guillotined");

        // ── Liquidate stock portfolio ──────────────────────────────────────────
        decimal stockProceeds = 0m;

        var portfolioDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetPortfolio",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        foreach (DataRow row in portfolioDt.Rows)
        {
            try
            {
                string ticker = row["Ticker"].ToString()!;
                int shares = int.Parse(row["Shares"].ToString()!);
                decimal price = decimal.Parse(row["CurrentPrice"].ToString()!);
                decimal proceeds = Math.Floor(price * shares);
                stockProceeds += proceeds;

                _sp.Select(Constants.Constants.discordBotConnStr, "SellStock",
                [
                    new SqlParameter("@UserID",    targetId),
                    new SqlParameter("@ServerID",  ServerId),
                    new SqlParameter("@Ticker",    ticker),
                    new SqlParameter("@Shares",    shares),
                    new SqlParameter("@PriceEach", price),
                    new SqlParameter("@TotalGain", proceeds)
                ]);
            }
            catch { /* non-fatal — skip bad row */ }
        }

        decimal totalSeized = seizedCredits + stockProceeds;

        // ── Get all server members to split across ─────────────────────────────
        var membersDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCreditLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        // Include target in denominator — they get nothing, but we count everyone
        // who has an account. Use Credits table directly for a full count.
        var recipientsDt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAllServerUsers",
            [new SqlParameter("@ServerID", ServerId)]);

        var recipients = recipientsDt.Rows.Cast<DataRow>()
            .Select(r => r["UserID"].ToString()!)
            .Where(uid => uid != targetId)
            .ToList();

        decimal share = recipients.Count > 0
            ? Math.Floor(totalSeized / recipients.Count)
            : 0m;

        if (share > 0m)
        {
            foreach (string uid in recipients)
            {
                try { _eco.AddCredits(uid, ServerId, share, "revolt_share"); }
                catch { }
            }
        }

        // ── Build announcement ─────────────────────────────────────────────────
        var revolterList = new StringBuilder();
        foreach (string uid in revolt.Revolters)
            revolterList.AppendLine($"• <@{uid}>");

        var resultEmbed = new EmbedBuilder()
            .WithTitle("🩸  The Guillotine Falls!")
            .WithColor(ColourRed)
            .WithDescription(
                $"**{target.Mention}** has been guillotined by the people!\n\n" +
                $"The revolt was led by:\n{revolterList}\n" +
                $"Their assets have been seized and distributed.")
            .AddField("💰 Credits Seized", CreditHelper.Format(seizedCredits), inline: true)
            .AddField("📈 Stocks Liquidated", CreditHelper.Format(stockProceeds), inline: true)
            .AddField("💎 Total Seized", CreditHelper.Format(totalSeized), inline: true)
            .AddField("👥 Recipients", $"{recipients.Count:N0} users", inline: true)
            .AddField("✂️ Share Each", CreditHelper.Format(share), inline: true)
            .WithColor(ColourGold)
            .WithCurrentTimestamp();

        // Post in the channel the command was used in
        await FollowupAsync(embed: resultEmbed.Build());

        // Also post in default channel if different
        try
        {
            var guild = Context.Guild;
            var serverDetails = ServerHelper.GetServerInfo(guild.Id);
            var channel = guild.GetTextChannel(ulong.Parse(serverDetails.DefaultChannelID));
            await channel.SendMessageAsync(embed: resultEmbed.Build());
        }
        catch { }
    }

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("❌  Revolt Failed")
            .WithColor(ColourGrey)
            .WithDescription(message)
            .WithFooter(Username, AvatarUrl)
            .Build(), ephemeral: true);
}