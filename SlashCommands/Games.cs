using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Data;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Central partial-class declaration for the /game command group.
/// Trivia   → Interaction.cs
/// Wordle   → Wordle.cs
/// Scramble → Scramble.cs
/// Poker    → Poker.cs
/// All converted to EF Core. `services` is threaded through for the background Task.Run
/// closures (scramble/wordle timeout reveals) that outlive the interaction's own scoped `db` —
/// same pattern as Audio.cs/Playlist.cs's CustomPlayer scope creation.
/// </summary>
[Group("game", "Play a minigame.")]
public partial class Games(DiscordbotContext db, IServiceProvider services) : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();

    private string Username  => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
}
