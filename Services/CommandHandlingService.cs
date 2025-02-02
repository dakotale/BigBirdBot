using Discord.Commands;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using DiscordBot.Helper;
using DiscordBot.Constants;
using System.Data;
using System.Data.SqlClient;

namespace DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;

        public CommandHandlingService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message))
                return;
            if (message.Source != MessageSource.User)
                return;

            // This value holds the offset where the prefix ends
            var argPos = 0;
            // Perform prefix check. You may want to replace this with
            // (!message.HasCharPrefix('!', ref argPos))
            // for a more traditional command format like !help.
            StoredProcedure stored = new StoredProcedure();
            string prefix = "";
            var channelId = message.Channel as SocketGuildChannel;
            bool isActive = false;
            DataTable dtPrefix = stored.Select(Constants.Constants.discordBotConnStr, "GetServerPrefixByServerID", new List<SqlParameter> { new SqlParameter("@ServerUID", Int64.Parse(channelId.Guild.Id.ToString())) });
            foreach (DataRow dr in dtPrefix.Rows)
            {
                prefix = dr["Prefix"].ToString();
                isActive = bool.Parse(dr["IsActive"].ToString());
            }

            // No Command for you, the server is inactive
            if (!isActive)
                return;

            if (!message.HasStringPrefix(prefix, ref argPos))
                return;

            var context = new SocketCommandContext(_discord, message);
            // Perform the execution of the command. In this method,
            // the command service will perform precondition and parsing check
            // then execute the command if one is matched.
            await _commands.ExecuteAsync(context, argPos, _services);
            // Note that normally a result will be returned by this format, but here
            // we will handle the result in CommandExecutedAsync,
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
                return;

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.
            if (result.IsSuccess)
            {
                Audit audit = new Audit();
                audit.InsertAudit(command.Value.Name, context.User.Id.ToString(), Constants.Constants.discordBotConnStr, context.Guild.Id.ToString());
                return;
            }


            if (result.ErrorReason.Contains("The input text has too few parameters"))
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "An additional parameter is required in order to run this command.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
            else if (result.ErrorReason.Contains("Failed to parse"))
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Please enter a whole number for this command.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
            else if (result.ErrorReason.Contains("Argument cannot be blank"))
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "The parameter(s) entered are not valid, please run the command with the correct parameters.", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
            else if (result.ErrorReason.Contains("The input text has too many parameters") && command.Value.Name.Equals("Play"))
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", "Your text search must be contained in quotes.\nExample: -play \"Video Search\"", Constants.Constants.errorImageUrl, "", Color.Red, "");
                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
            else
            {
                EmbedHelper embedHelper = new EmbedHelper();
                var embed = embedHelper.BuildMessageEmbed("BigBirdBot - Error", result.ErrorReason, Constants.Constants.errorImageUrl, "", Color.Red, "");
                await context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }
    }
}
