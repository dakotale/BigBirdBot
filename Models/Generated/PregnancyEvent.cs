using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PregnancyEvent
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime BirthAt { get; set; }

    public bool IsBorn { get; set; }

    public DateTime? BornAt { get; set; }

    public DateOnly? LastChildSupport { get; set; }
}
