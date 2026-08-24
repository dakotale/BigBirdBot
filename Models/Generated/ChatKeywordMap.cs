using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ChatKeywordMap
{
    public int Id { get; set; }

    public string AddKeyword { get; set; } = null!;

    public long ServerId { get; set; }

    public DateTime CreatedOn { get; set; }

    public string? CreatedBy { get; set; }

    public string? Keyword { get; set; }
}
