using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class PetJournal
{
    public int JournalId { get; set; }

    public int PetId { get; set; }

    public string Event { get; set; } = null!;

    public string Details { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Pet Pet { get; set; } = null!;
}
