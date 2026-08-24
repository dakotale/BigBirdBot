using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class BlackjackGame
{
    public string UserId { get; set; } = null!;

    public string MessageId { get; set; } = null!;

    public string Deck { get; set; } = null!;

    public string Player { get; set; } = null!;

    public string Dealer { get; set; } = null!;

    public bool Doubled { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Bet { get; set; }
}
