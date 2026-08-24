using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Music
{
    public int MusicId { get; set; }

    public long ServerUid { get; set; }

    public string VideoId { get; set; } = null!;

    public string Author { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = null!;
}
