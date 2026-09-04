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
    /// <summary>Schedules a one-off DM reminder. Replaces <c>AddReminder</c>.</summary>
    public async Task AddReminderAsync(string userId, string message, DateTime remindAtUtc)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        db.Reminders.Add(new Reminder
        {
            UserId = userId,
            Message = message,
            RemindAtUtc = remindAtUtc
        });

        await db.SaveChangesAsync();
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
    /// Registers a birthday: one row per year for the next 9 years (this year through +8), so
    /// the exact-date match in <see cref="GetTodaysBirthdaysAsync"/> fires once annually with
    /// no wraparound logic. Replaces <c>AddBirthday</c>.
    /// </summary>
    public async Task AddBirthdayAsync(DateTime birthdayDate, string birthdayUser, string birthdayGuild, string? birthdayChannel)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        for (int year = 0; year <= 8; year++)
        {
            db.Birthdays.Add(new Birthday
            {
                BirthdayDate = birthdayDate.AddYears(year),
                BirthdayUser = birthdayUser,
                BirthdayGuild = birthdayGuild,
                BirthdayChannel = birthdayChannel
            });
        }

        await db.SaveChangesAsync();
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
