using System;
using System.Collections.Generic;

namespace DiscordBot.Models.Generated;

/// <summary>
/// Not originally scaffolded — the /playlist feature (SlashCommands/Playlist.cs) had no
/// backing table or stored procedures in SQL Server at all (verified: zero matches for
/// "%Playlist%" in both sys.procedures and sys.tables), so every /playlist command has been
/// broken since it was written. This table was created fresh in Postgres as part of the EF
/// Core conversion, shaped to match exactly what Playlist.cs's stored-proc parameters implied:
/// one row per track per saved playlist, ordered by Position.
/// </summary>
public partial class PlaylistTrack
{
    public string UserId { get; set; } = null!;

    public string ServerId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int Position { get; set; }

    public string TrackTitle { get; set; } = null!;

    public string TrackUri { get; set; } = null!;
}
