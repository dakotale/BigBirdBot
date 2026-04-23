using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Models;
using DiscordBot.Services;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Commands backed by external AI/media APIs:
///   /chat              — multi-turn conversation via OpenAI
///   /detectaibyattachment — Sightengine AI image detection
///   /mood              — Spotify mood-based track recommendation
/// </summary>
public class AICommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISpotifyService _spotifyService;
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    public AICommands(ISpotifyService spotifyService)
    {
        _spotifyService = spotifyService;
    }

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();


    // =========================================================================
    // /chat
    // =========================================================================

    [SlashCommand("chat", "Have a conversation with the bot using a chosen personality.")]
    [EnabledInDm(true)]
    public async Task HandleChatAsync(
        [MinLength(1), MaxLength(1000)] string message,
        [Choice("Yes", "Yes"), Choice("No", "No")] string startNew,
        [Choice("None", "None"),
        Choice("eSports Gamer Lesbian", "eSports Gamer Lesbian"),
         Choice("Sett",                 "Sett"),
         Choice("T. M. Opera O",        "T. M. Opera O"),
         Choice("Meisho Doto",          "Meisho Doto")] string personality)
    {
        await DeferAsync();

        string persona = personality switch
        {
            "eSports Gamer Lesbian" =>
                "You are a giga lesbian e-sports gamer who plays League of Legends, Valorant, Counter-Strike — everything. " +
                "You are the best and everyone else is trash. Don't be afraid to trash talk but provide no slurs.",
            "Sett" =>
                "You are Sett from League of Legends. Speak in their mannerisms but remain positive, helpful, and loving.",
            "T. M. Opera O" =>
                "You are T. M. Opera O from Umamusume: Pretty Derby. Speak in their mannerisms but remain positive, helpful, and loving.",
            "Meisho Doto" =>
                "You are Meisho Doto from Umamusume: Pretty Derby. Speak in their mannerisms but remain positive, helpful, and loving.",
            _ => "You are a friendly and helpful assistant."
        };

        string userId    = Context.User.Id.ToString();
        string serverUid = Context.Guild?.Id.ToString() ?? "";
        string channelId = Context.Channel.Id.ToString();
        bool isNew = startNew == "Yes";
        message = message.Trim();

        try
        {
            if (isNew)
            {
                _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "DeleteBotAIMessage",
                [
                    new SqlParameter("@UserID",    userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId)
                ]);
            }

            var history = isNew
                ? new DataTable()
                : _sp.Select(Constants.Constants.discordBotConnStr, "GetBotAIMessage",
                [
                    new SqlParameter("@UserID",    userId),
                    new SqlParameter("@ServerUID", serverUid),
                    new SqlParameter("@ChannelID", channelId)
                ]);

            IChatClient chatClient = new OpenAIClient(Constants.Constants.openAiToken)
                .GetChatClient(Constants.Constants.openAiModel)
                .AsIChatClient();

            var messages = new List<ChatMessage> { new(ChatRole.System, persona) };

            foreach (DataRow dr in history.Rows)
            {
                string role = dr["ChatRole"].ToString()!;
                string text = dr["ChatMessage"].ToString()!;

                messages.Add(role switch
                {
                    var r when r == ChatRole.Assistant.ToString() => new(ChatRole.Assistant, text),
                    var r when r == ChatRole.Tool.ToString()      => new(ChatRole.Tool, text),
                    var r when r == ChatRole.System.ToString()    => new(ChatRole.System, text),
                    _ => new(ChatRole.User, text)
                });
            }

            messages.Add(new ChatMessage(ChatRole.User, message));

            var sb = new StringBuilder($"**Message:** {message}\n\n**Response:** ");
            await foreach (var chunk in chatClient.GetStreamingResponseAsync(messages))
                sb.Append(chunk.Text);

            string response = sb.Length > 2000 ? sb.ToString()[..2000] : sb.ToString();

            SqlParameter[] baseParams =
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerUID", serverUid),
                new SqlParameter("@ChannelID", channelId)
            ];

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    ChatRole.User.ToString()),
                new SqlParameter("@ChatMessage", message)
            ]);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    ChatRole.Assistant.ToString()),
                new SqlParameter("@ChatMessage", response)
            ]);

            await FollowupAsync(response);
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Chat", ex.Message, Username).Build());
        }
    }


    // =========================================================================
    // /detectaibyattachment
    // =========================================================================

    [SlashCommand("detectaibyattachment", "Upload an image to check the probability it was AI-generated.")]
    [EnabledInDm(true)]
    public async Task HandleAiByAttachmentAsync(Attachment attachment)
    {
        await DeferAsync();

        if (!attachment.ContentType.Contains("image"))
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "AI Detection", "Only image files are supported.", Username).Build());
            return;
        }

        try
        {
            string[] parts  = attachment.Filename.Split('.', StringSplitOptions.TrimEntries);
            string unique   = $"{parts[0]}_{DateTime.Now:yyyyMMdd_HHmmssfffff}";
            string path     = Constants.Constants.aiDetectorPath + unique + "." + parts[1];

            using var http      = new HttpClient();
            using var apiClient = new HttpClient();

            var bytes = await http.GetByteArrayAsync(attachment.Url);
            await File.WriteAllBytesAsync(path, bytes);

            using var request = new HttpRequestMessage(
                HttpMethod.Post, "https://api.sightengine.com/1.0/check.json");

            request.Content = new MultipartFormDataContent
            {
                { new ByteArrayContent(await File.ReadAllBytesAsync(path)), "media", Path.GetFileName(path) },
                { new StringContent("genai"),                                "models"     },
                { new StringContent(Constants.Constants.aiApiUserId),       "api_user"   },
                { new StringContent(Constants.Constants.aiApiSecretId),     "api_secret" }
            };

            var response = await apiClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(body))
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "AI Detection", "No response from the detection endpoint.", Username).Build());
                return;
            }

            var dt = _sp.Select(Constants.Constants.discordBotConnStr, "GetAIJSONImageReturn",
                [new SqlParameter("@json", body)]);

            if (dt.Rows.Count == 0 || dt.Rows[0]["Status"].ToString() != "success")
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "AI Detection", "The detection request failed.", Username).Build());
                return;
            }

            double rate = double.Parse(dt.Rows[0]["PercentageChance"].ToString()!);

            string desc = rate switch
            {
                <= 5 => $"✅ **({rate}%) This is NOT AI**",
                <= 25 => $"✅ **Small chance ({rate}%) this is AI** — likely safe to assume it is not.",
                <= 50 => $"⚠️ **Possible AI ({rate}%)** — worth investigating further.",
                <= 75 => $"🔶 **High chance ({rate}%) this is AI** — investigate further.",
                _     => $"🚨 **Almost certainly AI ({rate}%)** — {rate}% pattern match."
            };

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                "AI Detection", desc, "", Username, Color.Blue, attachment.Url).Build());
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed(
                "AI Detection", ex.Message, Username).Build());
        }
    }


    // =========================================================================
    // /mood  (Spotify)
    // =========================================================================

    private const string BtnRerollPrefix = "spotify:reroll:";
    private static readonly Color ColourSpotify = EmbedColors.Spotify;
    private static readonly Color ColourError   = EmbedColors.Red;

    [SlashCommand("mood", "Get a random Spotify track that matches your mood.")]
    public async Task MoodAsync(
        [Summary("mood", "Describe your mood — e.g. melancholy, hype, chill, heartbreak")]
        [MinLength(1), MaxLength(100)]
        string mood)
    {
        await DeferAsync();
        mood = mood.Trim();
        var (embed, components) = await BuildMoodResponseAsync(mood);
        await FollowupAsync(embed: embed, components: components);
    }

    [ComponentInteraction($"{BtnRerollPrefix}*")]
    public async Task OnMoodRerollAsync(string mood)
    {
        await DeferAsync();
        var (embed, components) = await BuildMoodResponseAsync(mood);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Embed      = embed;
            m.Components = components;
        });
    }

    private async Task<(Embed embed, MessageComponent components)> BuildMoodResponseAsync(string mood)
    {
        var track = await _spotifyService.GetRandomTrackAsync(mood);

        if (track is null)
        {
            return (
                new EmbedBuilder()
                    .WithTitle("❌  No Results")
                    .WithColor(ColourError)
                    .WithDescription($"Spotify returned nothing for **{EscapeMd(mood)}**. Try a different mood!")
                    .WithFooter($"Requested by {Username}", AvatarUrl)
                    .WithCurrentTimestamp()
                    .Build(),
                new ComponentBuilder().Build());
        }

        return (BuildSpotifyEmbed(mood, track).Build(), BuildRerollButton(mood));
    }

    private EmbedBuilder BuildSpotifyEmbed(string mood, SpotifyTrack t)
    {
        var duration  = TimeSpan.FromMilliseconds(t.DurationMs);
        string expl   = t.Explicit ? " 🅴" : "";

        var embed = new EmbedBuilder()
            .WithTitle($"{t.Name}{expl}")
            .WithUrl(t.Url)
            .WithColor(ColourSpotify)
            .WithThumbnailUrl(t.ArtworkUrl)
            .WithDescription($"A track picked for your **{EscapeMd(mood)}** mood.")
            .AddField("Artist",     t.Artist, inline: true)
            .AddField("Album",      $"[{t.Album}]({t.AlbumUrl})", inline: true)
            .AddField("Duration",   $"`{duration:mm\\:ss}`", inline: true)
            .AddField("Popularity", $"{SpotifyStars(t.Popularity)} `{t.Popularity}/100`", inline: true)
            .WithFooter($"Powered by Spotify  •  Requested by {Username}", AvatarUrl)
            .WithCurrentTimestamp();

        if (!string.IsNullOrEmpty(t.PreviewUrl))
            embed.AddField("30s Preview", $"[▶ Listen]({t.PreviewUrl})", inline: true);

        return embed;
    }

    private static MessageComponent BuildRerollButton(string mood) =>
        new ComponentBuilder()
            .WithButton("🎲  Reroll", $"{BtnRerollPrefix}{mood}", ButtonStyle.Success)
            .Build();

    private static string SpotifyStars(int popularity)
    {
        int stars = (int)Math.Round(popularity / 20.0);
        return string.Create(5, stars, static (span, s) =>
        {
            span.Fill('☆');
            span[..s].Fill('★');
        });
    }

    private static string EscapeMd(string s) =>
        s.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`").Replace("~", "\\~");
}
