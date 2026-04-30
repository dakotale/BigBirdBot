using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Data;
using Microsoft.Data.SqlClient;

namespace DiscordBot.SlashCommands;

/// <summary>
/// Commands backed by external AI/media APIs:
///   /chat              — multi-turn conversation via Claude
///   /detectaibyattachment — Sightengine AI image detection
///   /mood              — Spotify mood-based track recommendation
/// </summary>
public class AICommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISpotifyService _spotifyService;
    private readonly IAIChatService _aiChatService;
    private readonly EmbedHelper _embed = new();
    private readonly StoredProcedure _sp = new();

    public AICommands(ISpotifyService spotifyService, IAIChatService aiChatService)
    {
        _spotifyService = spotifyService;
        _aiChatService = aiChatService;
    }

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();


    // =========================================================================
    // /chat
    // =========================================================================

    [SlashCommand("chat", "Have a conversation with the bot using a chosen personality.")]
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
    public async Task HandleChatAsync(
        [Summary("message", "Your message to the bot"), MinLength(1), MaxLength(1000)] string message,
        [Summary("new-conversation", "Start fresh, clearing previous history"), Choice("Yes", "Yes"), Choice("No", "No")] string startNew,
        [Summary("personality", "Choose a persona for the bot"),
         Choice("None",                                          "None"),
         Choice("Bisexual Support Guide — bi-affirming guide",   "Bisexual Support Guide"),
         Choice("Cottagecore Witch — cozy, whimsical, nature-y", "Cottagecore Witch"),
         Choice("Gay Support Guide — gay & lesbian-affirming guide", "Gay Support Guide"),
         Choice("Meisho Doto — Umamusume: Pretty Derby",         "Meisho Doto"),
         Choice("Queer Support Guide — queer-affirming guide",    "Queer Support Guide"),
         Choice("Sett — League of Legends",                      "Sett"),
         Choice("T. M. Opera O — Umamusume: Pretty Derby",       "T. M. Opera O"),
         Choice("Transfirmation — trans-affirming support guide", "Transfirmation"),
         Choice("Vi — League of Legends / Arcane",               "Vi")] string personality)
    {
        await DeferAsync();

        string persona = PersonaHelper.ResolvePersona(personality);

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

            var historyPairs = history.Rows.Cast<DataRow>()
                .Select(dr => (Role: dr["ChatRole"].ToString()!, Text: dr["ChatMessage"].ToString()!));

            string aiText = await _aiChatService.GetResponseAsync(persona, historyPairs, message);

            SqlParameter[] baseParams =
            [
                new SqlParameter("@UserID",    userId),
                new SqlParameter("@ServerUID", serverUid),
                new SqlParameter("@ChannelID", channelId)
            ];

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    "user"),
                new SqlParameter("@ChatMessage", message)
            ]);

            _sp.UpdateCreate(Constants.Constants.discordBotConnStr, "AddBotAIMessage",
            [
                .. baseParams,
                new SqlParameter("@ChatRole",    "assistant"),
                new SqlParameter("@ChatMessage", aiText)
            ]);

            string header = personality == "None"
                ? $"**Message:** {message}\n\n**Response:** "
                : $"**Personality:** {personality}\n**Message:** {message}\n\n**Response:** ";
            string discordOutput = header + aiText;
            discordOutput = discordOutput.Length > 2000 ? discordOutput[..2000] : discordOutput;

            await FollowupAsync(discordOutput);
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
    [CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
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
