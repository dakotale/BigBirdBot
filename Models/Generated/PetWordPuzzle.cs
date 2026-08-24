using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PetWordPuzzle
{
    public int PuzzleId { get; set; }

    public string ChannelId { get; set; } = null!;

    public string Word { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool Claimed { get; set; }
}
