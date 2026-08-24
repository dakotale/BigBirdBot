using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditUserLeft
{
    public int Id { get; set; }

    public long UserUid { get; set; }

    public long ServerUid { get; set; }

    public DateTime LeftOn { get; set; }
}
