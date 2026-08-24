using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class UserInventory
{
    public int InventoryId { get; set; }

    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string ItemKey { get; set; } = null!;

    public int Quantity { get; set; }

    public DateTime AcquiredAt { get; set; }
}
