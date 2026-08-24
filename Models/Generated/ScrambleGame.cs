using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ScrambleGame
{
    public string ChannelId { get; set; } = null!;

    public string MessageId { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public string Difficulty { get; set; } = null!;

    public string StartedBy { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
}
