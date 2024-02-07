using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Victoria.Node;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System.Data;

namespace DiscordBot.Services
{
    public class InteractionHandlerService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _handler;
        private readonly IServiceProvider _services;
        private readonly LavaNode _lavaNode;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService handler, IServiceProvider services, LavaNode lavaNode)
        {
            _client = client;
            _handler = handler;
            _services = services;
            _lavaNode = lavaNode;
        }

        public async Task InitializeAsync()
        {
            // Process when the client is ready, so we can register our commands.
            _client.Ready += ReadyAsync;

            // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
            await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Process the InteractionCreated payloads to execute Interactions commands
            _client.InteractionCreated += HandleInteraction;
        }

        private async Task ReadyAsync()
        {
            var commands =
            //await _handler.RegisterCommandsToGuildAsync(_configuration.GetValue<ulong>("testGuild"));
            await _handler.RegisterCommandsGloballyAsync();

            foreach (var command in commands)
                _ = _services.GetRequiredService<LoggingService>().DebugAsync($"Name:{command.Name} Type.{command.Type} loaded");

            if (!_lavaNode.IsConnected)
                await _lavaNode.ConnectAsync();

            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetPlayerConnected", new List<SqlParameter>());

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    string voiceChannelId = dr["VoiceChannelID"].ToString();
                    string textChannelId = dr["TextChannelID"].ToString();
                    foreach (var guild in _client.Guilds)
                    {
                        var voiceChannel = guild.VoiceChannels.Where(s => s.Id.ToString().Equals(voiceChannelId)).FirstOrDefault();
                        var textChannel = guild.TextChannels.Where(s => s.Id.ToString().Equals(textChannelId)).FirstOrDefault();
                        if (voiceChannel != null && textChannel != null)
                        {
                            if (voiceChannel.ConnectedUsers.Count > 0)
                            {
                                await _lavaNode.JoinAsync(voiceChannel, textChannel);
                                Console.WriteLine($"{guild.Name} Player joined successfully");
                            }
                            else
                            {
                                Console.WriteLine($"No Connected Users for {voiceChannel.Name} in {guild.Name} so the bot will not join.");
                            }
                        }
                    }
                }
            }
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
                var context = new SocketInteractionContext(_client, interaction);

                // Execute the incoming command.
                var result = await _handler.ExecuteCommandAsync(context, _services);

                if (result.IsSuccess)
                {
                    if (interaction.Type is InteractionType.ApplicationCommand)
                    {
                        var command = context.Interaction as SocketSlashCommand;
                        var commandName = command.CommandName;
                        Audit audit = new Audit();
                        audit.InsertAudit(commandName, context.User.Id.ToString(), Constants.Constants.discordBotConnStr, context.Guild.Id.ToString());
                        return;
                    }
                }

                if (!result.IsSuccess)
                    switch (result.Error)
                    {
                        case InteractionCommandError.UnmetPrecondition:
                            // implement
                            break;
                    }

                if (!_lavaNode.IsConnected)
                    await _lavaNode.ConnectAsync();
            }
            catch
            {
                // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
                // response, or at least let the user know that something went wrong during the command execution.
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
            }
        }
    }
}
