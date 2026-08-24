using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class FishLog
{
    public int LogId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string FishName { get; set; } = null!;

    public string Rarity { get; set; } = null!;

    public decimal Credits { get; set; }

    public DateTime CreatedAt { get; set; }
}
