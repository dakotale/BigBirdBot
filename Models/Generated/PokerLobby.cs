using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PokerLobby
{
    public int GameId { get; set; }

    public string ChannelId { get; set; } = null!;

    public string MessageId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public decimal BetPerPlayer { get; set; }

    public string Deck { get; set; } = null!;

    public string Community { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
