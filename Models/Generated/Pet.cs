using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Pet
{
    public int PetId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Species { get; set; } = null!;

    public int Xp { get; set; }

    public int Hunger { get; set; }

    public int Happiness { get; set; }

    public int Energy { get; set; }

    public int Hygiene { get; set; }

    public bool IsActive { get; set; }

    public bool IsHibernating { get; set; }

    public string Accessory1 { get; set; } = null!;

    public string Accessory2 { get; set; } = null!;

    public DateTime? LastFed { get; set; }

    public DateTime? LastPetted { get; set; }

    public DateTime? LastGroomed { get; set; }

    public DateTime? LastPlayed { get; set; }

    public DateTime? LastSlept { get; set; }

    public DateTime BirthDate { get; set; }

    public DateTime? HibernatedAt { get; set; }

    public DateTime? ExploreReturnsAt { get; set; }

    public string? ExploreRewardKey { get; set; }

    public string Breed { get; set; } = null!;

    public string Bio { get; set; } = null!;

    public string? PictureUrl { get; set; }

    public virtual ICollection<PetJournal> PetJournals { get; set; } = new List<PetJournal>();
}
