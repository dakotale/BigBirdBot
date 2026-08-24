using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Server
{
    public int ServerId { get; set; }

    public long ServerUid { get; set; }

    public string ServerName { get; set; } = null!;

    public long? DefaultChannelId { get; set; }

    public int Volume { get; set; }

    public bool FixEmbed { get; set; }

    public bool IsPlayerConnected { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedOn { get; set; }

    public bool AnnouncementsEnabled { get; set; }
}
