using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Central partial-class declaration for the /game command group.
/// Trivia  → Interaction.cs
/// Wordle  → Wordle.cs
/// Scramble → Scramble.cs
/// Poker   → Poker.cs
/// </summary>
[Group("game", "Play a minigame.")]
public partial class Games : InteractionModuleBase<SocketInteractionContext>
{
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    private string Username  => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();
}
