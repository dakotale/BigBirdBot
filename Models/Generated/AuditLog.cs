using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditLog
{
    public int AuditLogId { get; set; }

    public string Command { get; set; } = null!;

    public long ServerUid { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;
}
