using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Birthday
{
    public int BirthdayId { get; set; }

    public DateTime BirthdayDate { get; set; }

    public string BirthdayUser { get; set; } = null!;

    public string BirthdayGuild { get; set; } = null!;

    public bool Sent { get; set; }

    public string? BirthdayChannel { get; set; }
}
