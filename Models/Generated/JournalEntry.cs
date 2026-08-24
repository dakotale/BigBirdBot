using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class JournalEntry
{
    public int EntryId { get; set; }

    public string UserId { get; set; } = null!;

    public DateOnly EntryDate { get; set; }

    public DateTime LoggedAt { get; set; }
}
