using Discord;
using Discord.Interactions;

namespace DiscordBot.Helper
{
    public class SupportGuildOnlyAttribute : PreconditionAttribute
    {
        public ulong[] Guild_Ids { get; }

        public SupportGuildOnlyAttribute(ulong[] GuildIds)
        {
            this.Guild_Ids = GuildIds;
        }

        public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
        {
            var guildUser = context.User as IGuildUser;
            if (guildUser == null || !Guild_Ids.Contains(guildUser.Guild.Id))
                return PreconditionResult.FromError("This command cannot be executed outside of another guild.");
            else
                return PreconditionResult.FromSuccess();
        }
    }
}
