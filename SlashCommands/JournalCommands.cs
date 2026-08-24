using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Data;
using DiscordBot.Helper;
using DiscordBot.Models.Generated;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.SlashCommands;

/// <summary>
/// /journal subcommands — daily journaling reminders and streak tracking, DM-only.
/// Reminder delivery itself happens in BotHost.RunSchedulerAsync.
/// </summary>
[Group("journal", "Daily journaling tools — DM only.")]
[CommandContextType(InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
public class JournalCommands(DiscordbotContext db) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();

    private static readonly Color JournalColor = new(0x7B68EE);

    /// <summary>
    /// Counts the user's current consecutive-day journaling streak, ending at
    /// <paramref name="today"/> if logged, or at yesterday if today isn't logged yet
    /// but the streak hasn't been broken (a whole day missed) — 0 if it has.
    ///
    /// Replaces the source SQL's gaps-and-islands window-function calculation
    /// (DATEDIFF(...) - ROW_NUMBER() ... WHERE Grp = 0), which has a confirmed off-by-one
    /// bug: verified empirically against a test user with 3 consecutive entries including
    /// today, the SQL reports Streak = 0 instead of 3. Both GetJournalStatus and
    /// LogJournalEntry shared that broken subquery, so both were affected. Computing it
    /// correctly here rather than porting the bug forward — flagged, not silently changed.
    /// </summary>
    private static int ComputeStreak(HashSet<DateOnly> entryDates, DateOnly today)
    {
        var cursor = today;
        if (!entryDates.Contains(cursor))
        {
            cursor = today.AddDays(-1);
            if (!entryDates.Contains(cursor))
                return 0;
        }

        int streak = 0;
        while (entryDates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    /// <summary>Subscribes the user to a daily journaling reminder DM at their chosen time.</summary>
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

        string userId = Context.User.Id.ToString();
        var existing = await db.JournalSubscriptions.FindAsync(userId);
        if (existing is not null)
        {
            existing.DailyTimeUtc = utcTime;
            existing.DailyTimeDisplay = displayTime;
            existing.SubscribedAt = DateTime.UtcNow;
            existing.LastReminderSentAt = null;
        }
        else
        {
            db.JournalSubscriptions.Add(new JournalSubscription
            {
                UserId = userId,
                DailyTimeUtc = utcTime,
                DailyTimeDisplay = displayTime
            });
        }
        await db.SaveChangesAsync();

        string prompt = JournalHelper.GetRandomPrompt();

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "📓  Daily Journal Reminder Set!",
            $"You'll receive a journaling reminder every day at **{displayTime}**.\n\n" +
            $"**A prompt to get you started today:**\n> *{prompt}*\n\n" +
            $"Once you finish writing, use `/journal done` to log your entry and track your streak!",
            JournalColor, footer: $"Reminders are sent here in DMs • {Username}").Build(), ephemeral: true);
    }


    /// <summary>Cancels the daily reminder DM, without affecting the user's saved streak/entries.</summary>
    [SlashCommand("unsubscribe", "Stop receiving daily journaling reminders.")]
    public async Task HandleUnsubscribeAsync()
    {
        await DeferAsync(ephemeral: true);

        var existing = await db.JournalSubscriptions.FindAsync(Context.User.Id.ToString());
        if (existing is not null)
        {
            db.JournalSubscriptions.Remove(existing);
            await db.SaveChangesAsync();
        }

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            "📓  Journal Reminders Cancelled",
            "You've unsubscribed from daily journal reminders.\n\n" +
            "Your streak and entries are still saved — you can re-subscribe any time with `/journal subscribe`.",
            Color.LightGrey, footer: Username).Build(), ephemeral: true);
    }


    /// <summary>Logs today's journal entry (once per day), advancing the streak, and shows 3 fresh prompts for next time.</summary>
    [SlashCommand("done", "Log today's journal entry and celebrate your progress!")]
    public async Task HandleDoneAsync()
    {
        await DeferAsync(ephemeral: true);

        string userId = Context.User.Id.ToString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        bool alreadyLogged = await db.JournalEntries
            .AnyAsync(e => e.UserId == userId && e.EntryDate == today);

        if (!alreadyLogged)
        {
            db.JournalEntries.Add(new JournalEntry { UserId = userId, EntryDate = today });
            await db.SaveChangesAsync();
        }

        var entryDates = (await db.JournalEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.EntryDate <= today)
            .Select(e => e.EntryDate)
            .ToListAsync()).ToHashSet();
        int streak = ComputeStreak(entryDates, today);

        if (alreadyLogged)
        {
            await FollowupAsync(embed: _embed.BuildSimpleEmbed(
                "📓  Already Logged Today",
                $"You've already logged today's journal entry!\n\n" +
                $"Your current streak is **{streak} {DayWord(streak)}**. Come back tomorrow to keep it going!",
                Color.Gold, footer: Username).Build(), ephemeral: true);
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

        await FollowupAsync(embed: _embed.BuildSimpleEmbed(
            $"{titleEmoji}  Journal Entry Logged — Great Work!",
            $"**Congratulations on journaling today, {Username}!**\n\n" +
            $"{streakMessage}\n\n" +
            $"**Some prompts to save for tomorrow:**\n{promptList}",
            Color.Green, footer: $"Keep it up — see you tomorrow! • {Username}").Build(), ephemeral: true);
    }


    /// <summary>Shows the user's current streak, total entries, and reminder subscription status.</summary>
    [SlashCommand("status", "Check your journaling streak and reminder schedule.")]
    public async Task HandleStatusAsync()
    {
        await DeferAsync(ephemeral: true);

        string userId = Context.User.Id.ToString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var subscription = await db.JournalSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

        int totalEntries = await db.JournalEntries.CountAsync(e => e.UserId == userId);

        bool hasSubscription = subscription is not null;

        if (!hasSubscription && totalEntries == 0)
        {
            await FollowupAsync(embed: NoSubscriptionEmbed().Build(), ephemeral: true);
            return;
        }

        var entryDates = (await db.JournalEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.EntryDate <= today)
            .Select(e => e.EntryDate)
            .ToListAsync()).ToHashSet();
        int streak = ComputeStreak(entryDates, today);

        string dailyTime = subscription?.DailyTimeDisplay ?? "Not set";

        string streakEmoji = streak switch
        {
            >= 30 => "🔥",
            >= 14 => "⭐",
            >= 7  => "✨",
            _     => "📝"
        };

        var eb = _embed.BuildSimpleEmbed(
            "📓  Your Journal Status", "", JournalColor, footer: Username,
            fields: [($"{streakEmoji} Current Streak", $"{streak} {DayWord(streak)}", true),
                     ("📖 Total Entries", totalEntries.ToString(), true)]);

        if (hasSubscription)
            eb.AddField("⏰ Daily Reminder", dailyTime, inline: true);
        else
            eb.AddField("⏰ Daily Reminder", "Not active — use `/journal subscribe` to set one", inline: false);

        await FollowupAsync(embed: eb.Build(), ephemeral: true);
    }


    /// <summary>Builds the "no active subscription" status embed.</summary>
    private EmbedBuilder NoSubscriptionEmbed() => _embed.BuildSimpleEmbed(
        "📓  Journal Status",
        "You don't have an active journal subscription yet.\n\n" +
        "Use `/journal subscribe` to set a daily reminder and start your journaling journey!",
        JournalColor, footer: Username);

    /// <summary>Pluralizes "day" for streak counts.</summary>
    private static string DayWord(int n) => n == 1 ? "day" : "days";
}
