using Discord;
using Discord.Interactions;

namespace DiscordBot.Helper
{
    public class SupportUserOnlyAttribute : PreconditionAttribute
    {
        public ulong[] User_Ids { get; }

        public SupportUserOnlyAttribute(ulong[] User_Ids)
        {
            this.User_Ids = User_Ids;
        }

        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            IGuildUser? guildUser = context.User as IGuildUser;
            if (guildUser == null || !User_Ids.Contains(guildUser.Id))
                return PreconditionResult.FromError("This command cannot be executed by this user.");
            else
                return PreconditionResult.FromSuccess();
        }
    }
}
