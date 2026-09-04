namespace DiscordBot.Data;

/// <summary>
/// One posting of the hourly bonus word puzzle in one channel. Table <c>dbo.PetWordPuzzle</c>
/// (name predates the pet system's removal — kept as-is since it's the live table name).
/// </summary>
public sealed class PetWordPuzzle
{
    public int PuzzleId { get; set; }
    public string ChannelId { get; set; } = "";
    public string Word { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool Claimed { get; set; }
}

/// <summary>One candidate word for the bonus word puzzle. Table <c>dbo.Words</c>.</summary>
public sealed class Word
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}
