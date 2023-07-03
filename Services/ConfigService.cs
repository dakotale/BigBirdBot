using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Helper;
using KillersLibrary.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Victoria;

namespace DiscordBot.Services
{
    public class ConfigService
    {
        public DiscordSocketClient _client = new DiscordSocketClient();

        public ConfigService(DiscordSocketClient client) 
        {
            _client = client;
        }

        public ServiceProvider ConfigureServices()
        {
            // Setup initial setup collection
            var services = new ServiceCollection();

            // Configure the DiscordSocketConfig with proper intents
            DiscordSocketConfig discordConfig = new DiscordSocketConfig();
            discordConfig.GatewayIntents = Discord.GatewayIntents.AllUnprivileged | Discord.GatewayIntents.MessageContent;

            // Add our additional singletons to return
            services.AddSingleton(discordConfig);
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<CommandService>();
            services.AddSingleton<CommandHandlingService>();
            services.AddSingleton<HttpClient>();

            services.AddLavaNode(x =>
            {
                x.SelfDeaf = true;
                x.Authorization = Constants.Constants.lavaLinkPwd;
                x.SocketConfiguration = new Victoria.WebSocket.WebSocketConfiguration { BufferSize = 2048, ReconnectAttempts = 10, ReconnectDelay = TimeSpan.FromSeconds(3) };
            });

            // Add our audio singletons for LavaLink
            services.AddSingleton<AudioService>();
            services.AddSingleton<SpotifyHelper>();
            services.AddSingleton<EmbedPagesService>();
            services.AddSingleton<MultiButtonsService>();

            // Need to AddLogging for Victoria to work properly or this will shit the 
            // bed saying that Logging is not enabled.
            services.AddLogging(builder => builder.AddConsole());

            return services.BuildServiceProvider();
        }
    }
}
