using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class AuditReactionAdded
{
    public int Id { get; set; }

    public string Emoji { get; set; } = null!;

    public long MessageUid { get; set; }

    public long UserUid { get; set; }

    public long ChannelUid { get; set; }

    public DateTime AddedOn { get; set; }
}
