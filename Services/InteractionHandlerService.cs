using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.Net.Extensions.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Players;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Services
{
    public class InteractionHandlerService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _handler;
        private readonly IServiceProvider _services;
        private readonly IAudioService _audioService;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService handler, IServiceProvider services, IAudioService audioService)
        {
            _client = client;
            _handler = handler;
            _services = services;
            _audioService = audioService;
        }

        public async Task InitializeAsync()
        {
            // Process when the client is ready, so we can register our commands.
            _client.Ready += ReadyAsync;

            // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
            await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            // Process the InteractionCreated payloads to execute Interactions commands
            _client.InteractionCreated += HandleInteraction;

            _handler.InteractionExecuted += HandleInteractionExecute;
        }

        private async Task ReadyAsync()
        {
            IReadOnlyCollection<Discord.Rest.RestGlobalCommand> commands =
            //await _handler.RegisterCommandsToGuildAsync(_configuration.GetValue<ulong>("testGuild"))
            await _handler.RegisterCommandsGloballyAsync();

            await _handler.RegisterCommandsAsync();

            foreach (Discord.Rest.RestGlobalCommand? command in commands)
                _ = _services.GetRequiredService<LoggingService>().DebugAsync($"Name:{command.Name} Type.{command.Type} loaded");


            StoredProcedure stored = new StoredProcedure();
            DataTable dt = stored.Select(Constants.Constants.discordBotConnStr, "GetPlayerConnected", new List<SqlParameter>());

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    string voiceChannelId = dr["VoiceChannelID"].ToString();
                    string textChannelId = dr["TextChannelID"].ToString();
                    foreach (SocketGuild? guild in _client.Guilds)
                    {
                        SocketVoiceChannel? voiceChannel = guild.VoiceChannels.Where(s => s.Id.ToString().Equals(voiceChannelId)).FirstOrDefault();
                        SocketTextChannel? textChannel = guild.TextChannels.Where(s => s.Id.ToString().Equals(textChannelId)).FirstOrDefault();
                        if (voiceChannel != null && textChannel != null)
                        {
                            if (voiceChannel.ConnectedUsers.Count > 0)
                            {
                                _ = Task.Run(async () =>
                                {
                                    await _audioService.StartAsync();
                                    await Task.Delay(3000);

                                    PlayerChannelBehavior channelBehavior = PlayerChannelBehavior.Join;
                                    CustomPlayerOptions options = new CustomPlayerOptions();
                                    options.SelfMute = true;
                                    options.TextChannel = textChannel;
                                    PlayerRetrieveOptions retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

                                    await _audioService.Players.JoinAsync<CustomPlayer, CustomPlayerOptions>(voiceChannel, CreatePlayerAsync, options);
                                });

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
                SocketInteractionContext context = new SocketInteractionContext(_client, interaction);

                // Execute the incoming command.
                IResult result = await _handler.ExecuteCommandAsync(context, _services);

                EmbedHelper embedHelper = new EmbedHelper();


                if (result.IsSuccess)
                {
                    if (interaction.Type is InteractionType.ApplicationCommand)
                    {
                        SocketSlashCommand? command = context.Interaction as SocketSlashCommand;
                        string commandName = command.CommandName;
                        Audit audit = new Audit();
                        audit.InsertAudit(commandName, context.User.Id.ToString(), Constants.Constants.discordBotConnStr, (context.Guild is null) ? context.Channel.Id.ToString() : context.Guild.Id.ToString());
                        audit.InsertAuditChannel(Constants.Constants.discordBotConnStr, (context.Guild is null) ? context.Channel.Id.ToString() : context.Guild.Id.ToString(), (context.Guild is null) ? context.Channel.Name : context.Guild.Name, context.User.Id.ToString());
                        return;
                    }
                }

                if (!result.IsSuccess)
                    switch (result.Error)
                    {
                        case InteractionCommandError.UnmetPrecondition:
                            await interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Unmet Precondition", $"**Unmet Precondition: {result.ErrorReason}**", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                            break;
                        case InteractionCommandError.BadArgs:
                            await interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Bad Arguments", $"**Invalid number of arguments.**", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                            break;
                        case InteractionCommandError.Exception:
                            await interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Command Exception", $"**Command exception: {result.ErrorReason}**", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                            break;
                        case InteractionCommandError.Unsuccessful:
                            await interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Unsuccessful", "**Command could not be executed.**", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                            break;
                        default:
                            break;
                    }
            }
            catch (Exception)
            {
                // If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
                // response, or at least let the user know that something went wrong during the command execution.
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
            }
        }

        private async Task HandleInteractionExecute(ICommandInfo commandInfo, IInteractionContext context, IResult result)
        {
            EmbedHelper embedHelper = new EmbedHelper();

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        await context.Interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Unmet Precondition", $"Unmet Precondition: {result.ErrorReason}", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                        break;
                    case InteractionCommandError.BadArgs:
                        await context.Interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Bad Arguments", $"Invalid number of arguments.", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                        break;
                    case InteractionCommandError.Exception:
                        await context.Interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Command Exception", $"Command exception: {result.ErrorReason}", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                        break;
                    case InteractionCommandError.Unsuccessful:
                        await context.Interaction.RespondAsync(embed: embedHelper.BuildMessageEmbed("BigBirdBot - Unsuccessful", "Command could not be executed.", Constants.Constants.errorImageUrl, context.User.Username, Discord.Color.Red).Build());
                        break;
                    default:
                        break;
                }
            }
        }

        private static ValueTask<CustomPlayer> CreatePlayerAsync(IPlayerProperties<CustomPlayer, CustomPlayerOptions> properties, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(properties);

            return ValueTask.FromResult(new CustomPlayer(properties));
        }
    }
}
