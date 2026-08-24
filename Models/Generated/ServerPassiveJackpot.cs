using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ServerPassiveJackpot
{
    public string ServerId { get; set; } = null!;

    public decimal Pool { get; set; }

    public DateTime LastUpdated { get; set; }
}
