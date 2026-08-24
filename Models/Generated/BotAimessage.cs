using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class BotAimessage
{
    public int BotAimessageId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerUid { get; set; } = null!;

    public string ChatRole { get; set; } = null!;

    public string ChatMessage { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string? ChannelId { get; set; }
}
