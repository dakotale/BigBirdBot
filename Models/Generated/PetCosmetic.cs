using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PetCosmetic
{
    public int CosmeticId { get; set; }

    public int PetId { get; set; }

    public string CosmeticType { get; set; } = null!;

    public string CosmeticKey { get; set; } = null!;

    public DateTime AppliedAt { get; set; }
}
