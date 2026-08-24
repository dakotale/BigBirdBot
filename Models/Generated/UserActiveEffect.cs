using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class UserActiveEffect
{
    public int EffectId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string EffectKey { get; set; } = null!;

    public DateTime? ExpiresAt { get; set; }

    public int StackCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
