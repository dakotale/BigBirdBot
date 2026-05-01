using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

[Group("journal", "Daily journaling tools — DM only.")]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
public class JournalCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly Color JournalColor = new(0x7B68EE);


    [SlashCommand("subscribe", "Sign up for a daily journaling reminder at your chosen time.")]
    public async Task HandleSubscribeAsync(
        [Summary("time", "Time for your daily reminder, e.g. '9:00 AM' or '21:00'")] string time,
        [Summary("utc_offset", "Your UTC offset, e.g. -5 for EST, -8 for PST, +1 for CET")] double utcOffset = 0)
    {
        await DeferAsync(ephemeral: true);

        if (!TimeOnly.TryParse(time, out TimeOnly parsedLocal))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "Journal Subscribe",
                "Couldn't parse that time. Try a format like `9:00 AM` or `21:00`.",
                Username).Build(), ephemeral: true);
            return;
        }

        double totalMinutesUtc = ((parsedLocal.Hour * 60 + parsedLocal.Minute) - utcOffset * 60 + 1440) % 1440;
        var utcTime = new TimeOnly((int)totalMinutesUtc / 60, (int)totalMinutesUtc % 60);

        string offsetLabel = utcOffset >= 0 ? $"UTC+{utcOffset}" : $"UTC{utcOffset}";
        string displayTime = $"{parsedLocal:h:mm tt} ({offsetLabel})";

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "UpsertJournalSubscription",
        [
            new SqlParameter("@UserID",           Context.User.Id.ToString()),
            new SqlParameter("@DailyTimeUtc",     utcTime.ToString("HH:mm:ss")),
            new SqlParameter("@DailyTimeDisplay", displayTime)
        ]);

        string prompt = JournalHelper.GetRandomPrompt();

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("📓  Daily Journal Reminder Set!")
            .WithColor(JournalColor)
            .WithDescription(
                $"You'll receive a journaling reminder every day at **{displayTime}**.\n\n" +
                $"**A prompt to get you started today:**\n> *{prompt}*\n\n" +
                $"Once you finish writing, use `/journal done` to log your entry and track your streak!")
            .WithFooter($"Reminders are sent here in DMs • {Username}")
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);
    }


    [SlashCommand("unsubscribe", "Stop receiving daily journaling reminders.")]
    public async Task HandleUnsubscribeAsync()
    {
        await DeferAsync(ephemeral: true);

        _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteJournalSubscription",
        [
            new SqlParameter("@UserID", Context.User.Id.ToString())
        ]);

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle("📓  Journal Reminders Cancelled")
            .WithColor(Color.LightGrey)
            .WithDescription(
                "You've unsubscribed from daily journal reminders.\n\n" +
                "Your streak and entries are still saved — you can re-subscribe any time with `/journal subscribe`.")
            .WithFooter(Username)
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);
    }


    [SlashCommand("done", "Log today's journal entry and celebrate your progress!")]
    public async Task HandleDoneAsync()
    {
        await DeferAsync(ephemeral: true);

        var result = _sp.Select(Constants.Constants.discordBotConnStr, "LogJournalEntry",
        [
            new SqlParameter("@UserID", Context.User.Id.ToString())
        ]);

        int streak = 1;
        bool alreadyLogged = false;

        if (result.Rows.Count > 0)
        {
            int.TryParse(result.Rows[0]["Streak"]?.ToString(), out streak);
            alreadyLogged = result.Rows[0]["AlreadyLogged"]?.ToString() == "1";
        }

        if (alreadyLogged)
        {
            await FollowupAsync(embed: new EmbedBuilder()
                .WithTitle("📓  Already Logged Today")
                .WithColor(Color.Gold)
                .WithDescription(
                    $"You've already logged today's journal entry!\n\n" +
                    $"Your current streak is **{streak} {DayWord(streak)}**. Come back tomorrow to keep it going!")
                .WithFooter(Username)
                .WithCurrentTimestamp()
                .Build(), ephemeral: true);
            return;
        }

        string streakMessage = streak switch
        {
            1  => "Every great journey starts with day one. You showed up — that's what matters.",
            2  => "Two days in a row! You're already building something real.",
            3  => "Three days strong. You're forming a habit.",
            7  => "One full week! That's a genuine commitment to yourself.",
            14 => "Two weeks of showing up for yourself. That's incredible.",
            30 => "**30 days.** A full month of journaling — that is something to be proud of.",
            _  when streak % 100 == 0 => $"**{streak} days!** You are an absolute journaling legend. Truly.",
            _  when streak >= 50 => $"**{streak} days** and counting. Your consistency is inspiring.",
            _  when streak >= 14 => $"**{streak} days!** Your dedication is genuinely impressive.",
            _  => $"You're on a **{streak}-day streak.** Keep the momentum going!"
        };

        string titleEmoji = streak switch
        {
            >= 30 => "🔥",
            >= 14 => "⭐",
            >= 7  => "✨",
            _     => "🎉"
        };

        var nextPrompts = JournalHelper.GetRandomPrompts(3);
        string promptList = string.Join("\n", nextPrompts.Select((p, i) => $"{i + 1}. *{p}*"));

        await FollowupAsync(embed: new EmbedBuilder()
            .WithTitle($"{titleEmoji}  Journal Entry Logged — Great Work!")
            .WithColor(Color.Green)
            .WithDescription(
                $"**Congratulations on journaling today, {Username}!**\n\n" +
                $"{streakMessage}\n\n" +
                $"**Some prompts to save for tomorrow:**\n{promptList}")
            .WithFooter($"Keep it up — see you tomorrow! • {Username}")
            .WithCurrentTimestamp()
            .Build(), ephemeral: true);
    }


    [SlashCommand("status", "Check your journaling streak and reminder schedule.")]
    public async Task HandleStatusAsync()
    {
        await DeferAsync(ephemeral: true);

        var result = _sp.Select(Constants.Constants.discordBotConnStr, "GetJournalStatus",
        [
            new SqlParameter("@UserID", Context.User.Id.ToString())
        ]);

        if (result.Rows.Count == 0)
        {
            await FollowupAsync(embed: NoSubscriptionEmbed().Build(), ephemeral: true);
            return;
        }

        var row = result.Rows[0];
        bool hasSubscription = row["HasSubscription"]?.ToString() == "True";
        int.TryParse(row["Streak"]?.ToString(), out int streak);
        int.TryParse(row["TotalEntries"]?.ToString(), out int totalEntries);
        string dailyTime = row["DailyTimeDisplay"]?.ToString() ?? "Not set";

        if (!hasSubscription && totalEntries == 0)
        {
            await FollowupAsync(embed: NoSubscriptionEmbed().Build(), ephemeral: true);
            return;
        }

        string streakEmoji = streak switch
        {
            >= 30 => "🔥",
            >= 14 => "⭐",
            >= 7  => "✨",
            _     => "📝"
        };

        var eb = new EmbedBuilder()
            .WithTitle("📓  Your Journal Status")
            .WithColor(JournalColor)
            .AddField($"{streakEmoji} Current Streak", $"{streak} {DayWord(streak)}", inline: true)
            .AddField("📖 Total Entries",  totalEntries.ToString(),                   inline: true);

        if (hasSubscription)
            eb.AddField("⏰ Daily Reminder", dailyTime, inline: true);
        else
            eb.AddField("⏰ Daily Reminder", "Not active — use `/journal subscribe` to set one", inline: false);

        eb.WithFooter(Username).WithCurrentTimestamp();

        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }


    private EmbedBuilder NoSubscriptionEmbed() => new EmbedBuilder()
        .WithTitle("📓  Journal Status")
        .WithColor(JournalColor)
        .WithDescription(
            "You don't have an active journal subscription yet.\n\n" +
            "Use `/journal subscribe` to set a daily reminder and start your journaling journey!")
        .WithFooter(Username)
        .WithCurrentTimestamp();

    private static string DayWord(int n) => n == 1 ? "day" : "days";
}
