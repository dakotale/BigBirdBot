using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditGuildJoined
{
    public int Id { get; set; }

    public long ServerUid { get; set; }

    public string ServerName { get; set; } = null!;

    public DateTime JoinedOn { get; set; }
}
