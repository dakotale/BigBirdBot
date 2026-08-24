using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class WordleGame
{
    public string ChannelId { get; set; } = null!;

    public string MessageId { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public string Guesses { get; set; } = null!;

    public string StartedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
