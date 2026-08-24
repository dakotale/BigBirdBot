using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class UsersScheduledKeyword
{
    public string UserId { get; set; } = null!;

    public string ChatKeyword { get; set; } = null!;

    public DateTime ScheduledDateTime { get; set; }
}
