using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class JackpotEntry
{
    public int EntryId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }
}
