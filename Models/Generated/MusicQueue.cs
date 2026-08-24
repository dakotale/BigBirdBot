using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class MusicQueue
{
    public int MusicQueueId { get; set; }

    public int MusicId { get; set; }

    public long ServerUid { get; set; }

    public long VoiceChannelId { get; set; }

    public long TextChannelId { get; set; }

    public string Url { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;
}
