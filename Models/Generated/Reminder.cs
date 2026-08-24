using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Reminder
{
    public int ReminderId { get; set; }

    public string UserId { get; set; } = null!;

    public string Message { get; set; } = null!;

    public DateTime RemindAtUtc { get; set; }

    public bool Sent { get; set; }
}
