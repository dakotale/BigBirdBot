using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ForgedCosmetic
{
    public int ForgeId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public int PetId { get; set; }

    public string Type { get; set; } = null!;

    public short Tier { get; set; }

    public string DisplayText { get; set; } = null!;

    public string ColourHex { get; set; } = null!;

    public decimal CreditsCost { get; set; }

    public DateTime CreatedAt { get; set; }
}
