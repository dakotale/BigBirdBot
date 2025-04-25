using Discord;
using Discord.Interactions;

namespace DiscordBot.Helper
{
    public class SupportGuildAndUserAttribute : PreconditionAttribute
    {
        public ulong[] Guild_Ids { get; }
        public ulong[] User_Ids { get; }

        public SupportGuildAndUserAttribute(ulong[] guild_Ids, ulong[] user_Ids)
        {
            Guild_Ids = guild_Ids;
            User_Ids = user_Ids;
        }

        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            IGuildUser? guildUser = context.User as IGuildUser;
            if (guildUser == null || !Guild_Ids.Contains(guildUser.Guild.Id) || !User_Ids.Contains(guildUser.Id))
                return PreconditionResult.FromError("This command cannot be executed outside of another guild or by this user.");
            else
                return PreconditionResult.FromSuccess();
        }
    }
}
