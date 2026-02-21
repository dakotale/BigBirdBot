using System.Data;
using System.Data.SqlClient;
using Discord;
using Discord.Interactions;
using Discord.Net.Extensions.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;

namespace DiscordBot.SlashCommands
{
    // GuildModule decoration limits these commands to only show by the guild below.
    [GuildModule(1443438808027889666)]
    public class ServerSpecific : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("addmcmods", "Add JAR files to the Minecraft files.")]
        [EnabledInDm(false)]
        public async Task HandleAnnouncement(Attachment attachment)
        {
            await DeferAsync(ephemeral: true);

            if (attachment.Filename.EndsWith(".jar"))
            {
                string path = Path.Combine(Constants.Constants.minecraftModsDirectory, attachment.Filename);
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(attachment.Url);
                    if (response.IsSuccessStatusCode)
                    {
                        using (var fs = new FileStream(path, FileMode.Create))
                        {
                            await response.Content.CopyToAsync(fs);
                        }
                        await FollowupAsync(embed: new EmbedBuilder().WithTitle("Mod Added").WithDescription($"Successfully added {attachment.Filename} to the Minecraft mods folder.  A server restart will be needed to reflect the new mods.").WithColor(Color.Green).Build(), ephemeral: true);
                    }
                    else
                    {
                        await FollowupAsync(embed: new EmbedBuilder().WithTitle("Error").WithDescription($"Failed to download {attachment.Filename}.").WithColor(Color.Red).Build(), ephemeral: true);
                    }
                }
            }
            else
            {
                await FollowupAsync(embed: new EmbedBuilder().WithTitle("Invalid File").WithDescription("Please upload a valid .jar file.").WithColor(Color.Red).Build(), ephemeral: true);
            }
        }
    }
}
