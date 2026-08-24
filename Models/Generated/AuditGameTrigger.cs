using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditGameTrigger
{
    public int Id { get; set; }

    public string Game { get; set; } = null!;

    public long UserUid { get; set; }

    public long ServerUid { get; set; }

    public DateTime TriggeredOn { get; set; }
}
