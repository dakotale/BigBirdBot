using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ChatKeywordAlias
{
    public int Id { get; set; }

    public string Alias { get; set; } = null!;

    public string Keyword { get; set; } = null!;

    public long ServerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;
}
