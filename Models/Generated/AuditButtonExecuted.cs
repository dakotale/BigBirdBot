using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditButtonExecuted
{
    public int Id { get; set; }

    public string ButtonId { get; set; } = null!;

    public long UserUid { get; set; }

    public long ServerUid { get; set; }

    public DateTime ExecutedOn { get; set; }
}
