using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using System.Data;
using System.Data.SqlClient;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Economy system — credit balance, earning, and transfer commands.
/// Credits are per-user per-server.
/// </summary>
public class Economy : InteractionModuleBase<SocketInteractionContext>
{
    private readonly StoredProcedure _sp = new();
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
    private string UserId => Context.User.Id.ToString();
    private string ServerId => Context.Guild?.Id.ToString() ?? "DM";

    private static readonly Color ColourGold = new(255, 215, 0);
    private static readonly Color ColourGreen = new(87, 242, 135);
    private static readonly Color ColourRed = new(237, 66, 69);
    private static readonly Color ColourBlue = new(88, 101, 242);


    [SlashCommand("balance", "Check your credit balance.")]
    [EnabledInDm(false)]
    public async Task HandleBalanceAsync(IUser? user = null)
    {
        await DeferAsync();

        var target = user ?? Context.User;
        var targetId = target.Id.ToString();

        EnsureAccount(targetId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   targetId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load balance."); return; }

        long balance = long.Parse(dt.Rows[0]["Balance"].ToString()!);
        long totalEarned = long.Parse(dt.Rows[0]["TotalEarned"].ToString()!);
        long totalSpent = long.Parse(dt.Rows[0]["TotalSpent"].ToString()!);

        bool isSelf = target.Id == Context.User.Id;

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  {target.Username}'s Balance")
            .WithColor(ColourGold)
            .WithThumbnailUrl(target.GetAvatarUrl())
            .AddField("Balance", CreditHelper.Format(balance), inline: true)
            .AddField("Total Earned", CreditHelper.Format(totalEarned), inline: true)
            .AddField("Total Spent", CreditHelper.Format(totalSpent), inline: true)
            .WithFooter(isSelf ? "Use /daily and /work to earn more!" : Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("daily", "Claim your daily credits!")]
    [EnabledInDm(false)]
    public async Task HandleDailyAsync()
    {
        await DeferAsync();
        EnsureAccount(UserId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load account."); return; }

        if (DateTime.TryParse(dt.Rows[0]["LastDaily"]?.ToString(), out var lastDaily))
        {
            var remaining = lastDaily.AddHours(CreditHelper.DailyCooldownHours) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle("⏳  Daily Already Claimed")
                    .WithColor(ColourRed)
                    .WithDescription(
                        $"You already claimed your daily today!\n\n" +
                        $"Come back in **{(int)remaining.TotalHours}h {remaining.Minutes}m**.")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
                return;
            }
        }

        // daily_boost: doubles payout, consumed immediately
        bool hasDailyBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "daily_boost");
        long dailyPayout = hasDailyBoost ? CreditHelper.DailyAmount * 2 : CreditHelper.DailyAmount;
        if (hasDailyBoost) ShopHelper.ConsumeActiveEffect(UserId, ServerId, "daily_boost");
        long newBalance = AddCredits(UserId, dailyPayout, "daily");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"🎁  Daily Claimed!")
            .WithColor(ColourGreen)
            .WithDescription(
                $"You claimed your daily {CreditHelper.Format(dailyPayout)}{(hasDailyBoost ? " 🎁 *(Daily Boost!)*" : "")}!\n\n" +
                $"New balance: {CreditHelper.Format(newBalance)}")
            .WithFooter($"{Username} • Come back in 24 hours!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("work", "Do some work to earn credits!")]
    [EnabledInDm(false)]
    public async Task HandleWorkAsync()
    {
        await DeferAsync();
        EnsureAccount(UserId);

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   UserId),
            new SqlParameter("@ServerID", ServerId)
        ]);

        if (dt.Rows.Count == 0) { await ErrorAsync("Could not load account."); return; }

        if (DateTime.TryParse(dt.Rows[0]["LastWork"]?.ToString(), out var lastWork))
        {
            var remaining = lastWork.AddMinutes(CreditHelper.WorkCooldownMinutes) - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await FollowupAsync(embed: new EmbedBuilder()
                    .WithTitle("⏳  Still Working")
                    .WithColor(ColourRed)
                    .WithDescription($"You're still on shift! Clock back in **{remaining.Minutes}m {remaining.Seconds}s**.")
                    .WithFooter(Username, AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build());
                return;
            }
        }

        long earned = Random.Shared.NextInt64(CreditHelper.WorkMin, CreditHelper.WorkMax + 1);
        // work_boost: 2× payout, decrements stack count (3 uses total)
        bool hasWorkBoost = ShopHelper.HasActiveEffect(UserId, ServerId, "work_boost");
        if (hasWorkBoost)
        {
            earned *= 2;
            ShopHelper.ConsumeActiveEffect(UserId, ServerId, "work_boost");
        }
        long newBalance = AddCredits(UserId, earned, "work");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("💼  You Worked!")
            .WithColor(ColourGreen)
            .WithDescription(
                CreditHelper.WorkMessage(earned) + (hasWorkBoost ? " 💼 *(Work Boost!)*" : "") + "\n\n" +
                $"Balance: {CreditHelper.Format(newBalance)}")
            .WithFooter($"{Username} • Come back in 1 hour!", AvatarUrl)
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("transfer", "Send credits to another user.")]
    [EnabledInDm(false)]
    public async Task HandleTransferAsync(
        SocketGuildUser recipient,
        [MinValue(1)] long amount)
    {
        await DeferAsync(ephemeral: true);

        if (recipient.Id == Context.User.Id)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer", "You can't transfer credits to yourself.", Username).Build(), ephemeral: true);
            return;
        }

        if (recipient.IsBot)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer", "You can't transfer credits to a bot.", Username).Build(), ephemeral: true);
            return;
        }

        EnsureAccount(UserId);
        string recipientId = recipient.Id.ToString();
        EnsureAccount(recipientId, ServerId);

        long senderBalance = GetBalance(UserId);

        if (amount > senderBalance)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Transfer",
                $"You don't have enough credits. Your balance: {CreditHelper.Format(senderBalance)}",
                Username).Build(), ephemeral: true);
            return;
        }

        long newSenderBalance    = DeductCredits(UserId, amount, "transfer_out");
        long newRecipientBalance = AddCredits(recipientId, ServerId, amount, "transfer_in");

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  Transfer Complete")
            .WithColor(ColourGreen)
            .WithDescription(
                $"Sent {CreditHelper.Format(amount)} to {recipient.Mention}.\n\n" +
                $"Your new balance: {CreditHelper.Format(newSenderBalance)}")
            .WithFooter(Username, AvatarUrl)
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);

        // Notify recipient via the channel (non-ephemeral follow-up in the channel)
        await Context.Channel.SendMessageAsync(embed: new EmbedBuilder()
            .WithTitle($"{CreditHelper.CurrencyEmoji}  Credits Received!")
            .WithColor(ColourGold)
            .WithDescription(
                $"{Context.User.Mention} sent {CreditHelper.Format(amount)} to {recipient.Mention}!\n" +
                $"{recipient.DisplayName}'s new balance: {CreditHelper.Format(newRecipientBalance)}")
            .WithCurrentTimestamp()
            .Build());
    }


    [SlashCommand("creditleaderboard", "Show the richest users in this server.")]
    [EnabledInDm(false)]
    public async Task HandleLeaderboardAsync()
    {
        await DeferAsync();

        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCreditLeaderboard",
            [new SqlParameter("@ServerID", ServerId)]);

        if (dt.Rows.Count == 0)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Leaderboard", "No credit data found for this server.", Username).Build());
            return;
        }

        var sb = new System.Text.StringBuilder();
        var medals = new[] { "🥇", "🥈", "🥉" };

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            string medal = i < 3 ? medals[i] : $"**{i + 1}.**";
            string userName = dt.Rows[i]["Username"].ToString()!;
            long bal = long.Parse(dt.Rows[i]["Balance"].ToString()!);
            sb.AppendLine($"{medal} **{userName}** — {CreditHelper.Format(bal)}");
        }

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"💰  Credit Leaderboard — {Context.Guild.Name}")
            .WithColor(ColourGold)
            .WithDescription(sb.ToString())
            .WithCurrentTimestamp()
            .Build());
    }


    private void EnsureAccount(string userId) =>
        EnsureAccount(userId, ServerId);

    private void EnsureAccount(string userId, string serverId) =>
        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "EnsureCreditAccount",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId)
        ]);

    /// <summary>Adds credits (slash command context — uses ServerId from Context).</summary>
    public long AddCredits(string userId, long amount, string source) =>
        AddCredits(userId, ServerId, amount, source);

    /// <summary>Adds credits with explicit serverId — safe to call from BotHost.</summary>
    public long AddCredits(string userId, string serverId, long amount, string source)
    {
        EnsureAccount(userId, serverId);
        var result = _sp.Select(Constants.Constants.discordBotConnStr, "AddCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId),
            new SqlParameter("@Amount",   amount),
            new SqlParameter("@Source",   source)
        ]);
        return result.Rows.Count > 0 ? long.Parse(result.Rows[0]["Balance"].ToString()!) : 0;
    }

    /// <summary>Deducts credits (slash command context).</summary>
    public long DeductCredits(string userId, long amount, string source) =>
        DeductCredits(userId, ServerId, amount, source);

    /// <summary>Deducts credits with explicit serverId — safe to call from BotHost.</summary>
    public long DeductCredits(string userId, string serverId, long amount, string source)
    {
        var result = _sp.Select(Constants.Constants.discordBotConnStr, "DeductCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId),
            new SqlParameter("@Amount",   amount),
            new SqlParameter("@Source",   source)
        ]);
        return result.Rows.Count > 0 ? long.Parse(result.Rows[0]["Balance"].ToString()!) : -1;
    }

    /// <summary>Gets balance (slash command context).</summary>
    public long GetBalance(string userId) =>
        GetBalance(userId, ServerId);

    /// <summary>Gets balance with explicit serverId — safe to call from BotHost.</summary>
    public long GetBalance(string userId, string serverId)
    {
        var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetCredits",
        [
            new SqlParameter("@UserID",   userId),
            new SqlParameter("@ServerID", serverId)
        ]);
        return dt.Rows.Count > 0 ? long.Parse(dt.Rows[0]["Balance"].ToString()!) : 0;
    }

    private async Task ErrorAsync(string message) =>
        await FollowupAsync(embed: _embed.BuildErrorEmbed("Economy", message, Username).Build());
}
