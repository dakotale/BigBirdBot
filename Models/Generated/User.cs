using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class User
{
    public string UserId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public DateTime JoinDate { get; set; }

    public long ServerUid { get; set; }

    public string? Nickname { get; set; }

    public int? PronounId { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime? DeletedOn { get; set; }

    public DateTime? LastSeen { get; set; }
}
