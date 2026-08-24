using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

public partial class Quote
{
    public int QuoteId { get; set; }

    public long GuildId { get; set; }

    public long AuthorId { get; set; }

    public string AuthorUsername { get; set; } = null!;

    public long SavedByUserId { get; set; }

    public string SavedByUsername { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string OriginalMessageUrl { get; set; } = null!;

    public string? ArchiveMessageUrl { get; set; }

    public string? AttachmentUrl { get; set; }

    public DateTime SavedAt { get; set; }
}
