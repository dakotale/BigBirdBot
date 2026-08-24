using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PlayerConnected
{
    public int PlayerId { get; set; }

    public long ServerUid { get; set; }

    public long VoiceChannelId { get; set; }

    public long TextChannelId { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;
}
