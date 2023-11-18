using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Victoria.Node.EventArgs;
using Victoria.Node;
using Victoria.Player;
using Discord;
using DiscordBot.Constants;
using System.Data.SqlClient;
using System.Text.Json;
using System.Data;

namespace DiscordBot.Services
{
    public sealed class AudioService 
    {
        private readonly LavaNode _lavaNode;
        private readonly ILogger _logger;
        public readonly HashSet<ulong> VoteQueue;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;

        public AudioService(LavaNode lavaNode, ILoggerFactory loggerFactory)
        {
            _lavaNode = lavaNode;
            _logger = loggerFactory.CreateLogger<LavaNode>();
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            VoteQueue = new HashSet<ulong>();
            _lavaNode.OnStatsReceived += OnStatsReceivedAsync;
            _lavaNode.OnWebSocketClosed += OnWebSocketClosedAsync;
            _lavaNode.OnTrackStart += OnTrackStartAsync;
            _lavaNode.OnTrackEnd += OnTrackEndAsync;
            _lavaNode.OnTrackStuck += OnTrackStuckAsync;
            _lavaNode.OnTrackException += OnTrackExceptionAsync;
        }

        private async Task OnTrackExceptionAsync(TrackExceptionEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it threw an exception.");
            await Task.CompletedTask;
        }

        private async Task OnTrackStuckAsync(TrackStuckEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            arg.Player.Vueue.Enqueue(arg.Track);
            await arg.Player.TextChannel.SendMessageAsync($"{arg.Track} has been requeued because it got stuck.");
            await Task.CompletedTask;
        }

        private async Task OnWebSocketClosedAsync(WebSocketClosedEventArg arg)
        {
            StoredProcedure storedProcedure = new StoredProcedure();
            List<SqlParameter> parameters = new List<SqlParameter>
            {
                new SqlParameter("@ServerID", Int64.Parse(arg.Guild.Id.ToString())),
                new SqlParameter("@Code", arg.Code),
                new SqlParameter("@Reason", arg.Reason)
            };

            storedProcedure.UpdateCreate(Constants.Constants.discordBotConnStr, "AddMusicLog", parameters);
            await Task.CompletedTask;
        }

        private Task OnStatsReceivedAsync(StatsEventArg arg)
        {
            _logger.LogInformation(JsonSerializer.Serialize(arg));
            return Task.CompletedTask;
        }

        private async Task OnTrackStartAsync(TrackStartEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            var embed = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - Start Playing",
                Color = Color.Blue,
                Description = $"Started playing **{arg.Track}**\nURL: {arg.Track.Url}\nDuration: {arg.Track.Duration}."
            };

            embed.WithCurrentTimestamp();

            await arg.Player.TextChannel.SendMessageAsync(embed: embed.Build());

            StoredProcedure procedure = new StoredProcedure();
            int volume = 100;

            try
            {
                DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetVolume", new List<SqlParameter>
                {
                    new SqlParameter("@ServerUID", long.Parse(arg.Player.VoiceChannel.Guild.Id.ToString()))
                });

                foreach (DataRow dr in dt.Rows)
                {
                    volume = int.Parse(dr["Volume"].ToString());
                }

                await arg.Player.SetVolumeAsync(volume);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                embed = new EmbedBuilder
                {
                    Title = $"BigBirdBot Music - Error",
                    Color = Color.Red,
                    ThumbnailUrl = Constants.Constants.errorImageUrl,
                    Description = $"{ex.Message}"
                };
            }
        }

        private async Task OnTrackEndAsync(TrackEndEventArg<LavaPlayer<LavaTrack>, LavaTrack> arg)
        {
            var embed = new EmbedBuilder
            {
                Title = $"BigBirdBot Music - Finished Playing",
                Color = Color.Blue,
                Description = $"Finished playing {arg.Track}\nURL: {arg.Track.Url}\nDuration: {arg.Track.Duration}\n\n**The bot will leave if inactive for 5 seconds, use -stay if you want the bot to stay in VC.**"
            };

            embed.WithCurrentTimestamp();

            if (arg.Player.Vueue.Count > 0)
            {
                int volume = 100;
                arg.Player.Vueue.TryDequeue(out var lavaTrack);

                StoredProcedure procedure = new StoredProcedure();
                try
                {
                    DataTable dt = procedure.Select(Constants.Constants.discordBotConnStr, "GetVolume", new List<SqlParameter>
                    {
                        new SqlParameter("@ServerUID", long.Parse(arg.Player.VoiceChannel.Guild.Id.ToString()))
                    });

                    foreach (DataRow dr in dt.Rows)
                    {
                        volume = int.Parse(dr["Volume"].ToString());
                    }

                    await arg.Player.SetVolumeAsync(volume);
                    await arg.Player.PlayAsync(lavaTrack);
                }
                catch (Exception ex)
                {
                    embed = new EmbedBuilder
                    {
                        Title = $"BigBirdBot Music - Error",
                        Color = Color.Red,
                        ThumbnailUrl = Constants.Constants.errorImageUrl,
                        Description = $"{ex.Message}"
                    };

                    await arg.Player.TextChannel.SendMessageAsync(embed: embed.Build());
                }
            }

            await Task.CompletedTask;
        }
    }
}
