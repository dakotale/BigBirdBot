using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PokerPlayer
{
    public int PlayerId { get; set; }

    public int GameId { get; set; }

    public string UserId { get; set; } = null!;

    public string Hand { get; set; } = null!;
}
