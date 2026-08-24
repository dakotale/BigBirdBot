using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class ChatKeyword
{
    public int Id { get; set; }

    public string ChatKeyword1 { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public bool Nsfw { get; set; }
}
