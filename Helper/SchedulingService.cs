using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for the two simple time-based scheduler features: one-off DM reminders
/// (<c>/remind</c>) and yearly birthday greetings (<c>/addbirthday</c>). Replaces
/// <c>AddReminder</c>/<c>GetDueReminders</c>/<c>AddBirthday</c>/<c>GetTodaysBirthdays</c>.
///
/// The two "get due X" procedures atomically marked-and-returned rows via an <c>UPDATE ...
/// OUTPUT</c> statement; EF Core's set-based <c>ExecuteUpdateAsync</c> can't return rows, so
/// both methods here follow the same select-then-update pattern already used by
/// <see cref="KeywordService.GetDueDeliveriesAsync"/> — a due row read here and marked sent a
/// few milliseconds later is not meaningfully different from the original single-statement
/// version for a scheduler that only ever runs one instance, ticking once a minute.
/// </summary>
public sealed class SchedulingService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Schedules a one-off DM reminder and returns its new id (shown to the user for <c>/reminddelete</c>). Replaces <c>AddReminder</c>.</summary>
    public async Task<int> AddReminderAsync(string userId, string message, DateTime remindAtUtc)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var reminder = new Reminder
        {
            UserId = userId,
            Message = message,
            RemindAtUtc = remindAtUtc
        };
        db.Reminders.Add(reminder);

        await db.SaveChangesAsync();
        return reminder.ReminderId;
    }

    /// <summary>A user's own not-yet-sent reminders, soonest first.</summary>
    public async Task<IReadOnlyList<PendingReminder>> GetPendingRemindersAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Reminders
            .Where(r => r.UserId == userId && !r.Sent)
            .OrderBy(r => r.RemindAtUtc)
            .Select(r => new PendingReminder(r.ReminderId, r.Message, r.RemindAtUtc))
            .ToListAsync();
    }

    /// <summary>Cancels one of a user's pending reminders. Returns false if it isn't theirs, doesn't exist, or already fired.</summary>
    public async Task<bool> CancelReminderAsync(string userId, int reminderId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        int removed = await db.Reminders
            .Where(r => r.ReminderId == reminderId && r.UserId == userId && !r.Sent)
            .ExecuteDeleteAsync();

        return removed > 0;
    }

    /// <summary>
    /// Finds every reminder due by now, marks them sent, and returns what to deliver.
    /// Replaces <c>GetDueReminders</c> (<c>GETUTCDATE()</c> comparison).
    /// </summary>
    public async Task<IReadOnlyList<DueReminder>> GetDueRemindersAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var now = DateTime.UtcNow;

        var due = await db.Reminders
            .Where(r => !r.Sent && r.RemindAtUtc <= now)
            .Select(r => new { r.ReminderId, r.UserId, r.Message })
            .ToListAsync();

        if (due.Count == 0) return Array.Empty<DueReminder>();

        var ids = due.Select(d => d.ReminderId).ToList();
        await db.Reminders.Where(r => ids.Contains(r.ReminderId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.Sent, true));

        return due.Select(d => new DueReminder(d.UserId, d.Message)).ToList();
    }

    /// <summary>
    /// Registers (or re-registers) a member's birthday: any existing rows for that member in the
    /// guild are dropped first, then one row is inserted per year for the next 9 occurrences,
    /// starting this year — or next year if this year's date has already passed. Feb 29 rolls to
    /// Feb 28 in common years. The exact-date match in <see cref="GetTodaysBirthdaysAsync"/> then
    /// fires once annually with no wraparound logic. Replaces <c>AddBirthday</c>.
    /// </summary>
    public async Task AddBirthdayAsync(int month, int day, string mention, string guildId, string? channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        // Idempotent re-registration — never stack duplicate announcements.
        await db.Birthdays
            .Where(b => b.BirthdayGuild == guildId && b.BirthdayUser == mention)
            .ExecuteDeleteAsync();

        var today = DateTime.Now.Date;
        int startYear = today.Year;
        var clampedThisYear = new DateTime(startYear, month, Math.Min(day, DateTime.DaysInMonth(startYear, month)));
        if (clampedThisYear < today) startYear++;

        for (int year = startYear; year < startYear + 9; year++)
        {
            db.Birthdays.Add(new Birthday
            {
                BirthdayDate = new DateTime(year, month, Math.Min(day, DateTime.DaysInMonth(year, month))),
                BirthdayUser = mention,
                BirthdayGuild = guildId,
                BirthdayChannel = channelId
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Every birthday registered in a guild, deduplicated to each member's next upcoming occurrence, soonest first.</summary>
    public async Task<IReadOnlyList<RegisteredBirthday>> GetGuildBirthdaysAsync(string guildId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var today = DateTime.Now.Date;

        var rows = await db.Birthdays
            .Where(b => b.BirthdayGuild == guildId && !b.Sent && b.BirthdayDate >= today)
            .ToListAsync();

        return rows
            .GroupBy(b => b.BirthdayUser)
            .Select(g => g.OrderBy(b => b.BirthdayDate).First())
            .Select(b => new RegisteredBirthday(b.BirthdayUser, b.BirthdayDate, b.BirthdayChannel))
            .OrderBy(r => (r.NextDate.Month, r.NextDate.Day))
            .ToList();
    }

    /// <summary>Removes every future row for a member's birthday in a guild. Returns the number of rows deleted.</summary>
    public async Task<int> RemoveBirthdayAsync(string guildId, string mention)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.Birthdays
            .Where(b => b.BirthdayGuild == guildId && b.BirthdayUser == mention)
            .ExecuteDeleteAsync();
    }

    // ── Per-user time zone (shared across servers; used by /remind) ───────────

    /// <summary>A user's saved time-zone token (IANA id or <c>UTC±HH:MM</c>), or null if they haven't set one.</summary>
    public async Task<string?> GetUserTimeZoneAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.UserTimezones
            .Where(t => t.UserId == userId)
            .Select(t => t.TimeZone)
            .FirstOrDefaultAsync();
    }

    /// <summary>Saves (or replaces) a user's time zone. <paramref name="timeZone"/> should already be a canonical token from <see cref="TimeZoneResolver"/>.</summary>
    public async Task SetUserTimeZoneAsync(string userId, string timeZone)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var row = await db.UserTimezones.FirstOrDefaultAsync(t => t.UserId == userId);
        if (row is null)
            db.UserTimezones.Add(new UserTimezone { UserId = userId, TimeZone = timeZone, UpdatedOn = DateTime.UtcNow });
        else
        {
            row.TimeZone = timeZone;
            row.UpdatedOn = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Clears a user's saved time zone. Returns whether a row existed.</summary>
    public async Task<bool> ClearUserTimeZoneAsync(string userId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        return await db.UserTimezones.Where(t => t.UserId == userId).ExecuteDeleteAsync() > 0;
    }

    /// <summary>
    /// Finds every not-yet-celebrated birthday whose date matches today, marks them sent, and
    /// returns what to announce. Replaces <c>GetTodaysBirthdays</c> (<c>GETDATE()</c>
    /// comparison — local time, unlike <see cref="GetDueRemindersAsync"/>'s UTC comparison).
    /// </summary>
    public async Task<IReadOnlyList<DueBirthday>> GetTodaysBirthdaysAsync()
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var today = DateTime.Now.Date;

        var due = await db.Birthdays
            .Where(b => !b.Sent && b.BirthdayDate.Date == today)
            .Select(b => new { b.BirthdayId, b.BirthdayUser, b.BirthdayGuild, b.BirthdayChannel })
            .ToListAsync();

        if (due.Count == 0) return Array.Empty<DueBirthday>();

        var ids = due.Select(d => d.BirthdayId).ToList();
        await db.Birthdays.Where(b => ids.Contains(b.BirthdayId)).ExecuteUpdateAsync(s => s.SetProperty(b => b.Sent, true));

        return due.Select(d => new DueBirthday(d.BirthdayUser, d.BirthdayGuild, d.BirthdayChannel)).ToList();
    }
}
