using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PetEgg
{
    public int EggId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public int Parent1Id { get; set; }

    public int Parent2Id { get; set; }

    public string Species { get; set; } = null!;

    public string Breed { get; set; } = null!;

    public int BaseHunger { get; set; }

    public int BaseHappiness { get; set; }

    public int BaseEnergy { get; set; }

    public int BaseHygiene { get; set; }

    public int BaseXp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime HatchAt { get; set; }

    public bool IsHatched { get; set; }

    public int? HatchedPetId { get; set; }
}
