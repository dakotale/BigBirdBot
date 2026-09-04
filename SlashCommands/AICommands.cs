using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Constants;
using DiscordBot.Helper;
using DiscordBot.Models;
using DiscordBot.Services;

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
    private readonly AIMessageService _messages;
    private readonly EmbedHelper _embed = new();

    /// <summary>Injects the Spotify, AI chat, and message-history backends used by /mood, /chat, and /detectaibyattachment respectively.</summary>
    public AICommands(ISpotifyService spotifyService, IAIChatService aiChatService, AIMessageService messages)
    {
        _spotifyService = spotifyService;
        _aiChatService = aiChatService;
        _messages = messages;
    }

    private string Username => Context.User.Username;
    private string AvatarUrl => Context.User.GetAvatarUrl();


    // =========================================================================
    // /chat
    // =========================================================================

    /// <summary>
    /// Sends the user's message to the AI backend with the chosen persona as system prompt,
    /// persisting both sides of the exchange as conversation history (unless starting fresh),
    /// and splits long replies across multiple follow-up messages beyond the embed limit.
    /// </summary>
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
                await _messages.DeleteHistoryAsync(userId, serverUid, channelId);

            var historyPairs = isNew
                ? []
                : await _messages.GetHistoryAsync(userId, serverUid, channelId);

            string aiText = await _aiChatService.GetResponseAsync(persona, historyPairs, message);

            await _messages.AddMessageAsync(userId, serverUid, channelId, "user", message);
            await _messages.AddMessageAsync(userId, serverUid, channelId, "assistant", aiText);

            string title = personality == "None" ? "Chat" : personality;
            string description = $"**Message:** {message}\n\n**Response:** {aiText}";

            const int embedLimit = 4096;
            const int messageLimit = 2000;

            await FollowupAsync(embed: _embed.BuildMessageEmbed(
                title,
                description.Length <= embedLimit ? description : description[..embedLimit],
                "", Username, Color.Blue).Build());

            string overflow = description.Length > embedLimit ? description[embedLimit..] : "";
            while (overflow.Length > 0)
            {
                string chunk = overflow[..Math.Min(messageLimit, overflow.Length)];
                await FollowupAsync(chunk);
                overflow = overflow[chunk.Length..];
            }
        }
        catch (Exception ex)
        {
            await FollowupAsync(embed: _embed.BuildErrorEmbed("Chat", ex.Message, Username).Build());
        }
    }


    // =========================================================================
    // /detectaibyattachment
    // =========================================================================

    /// <summary>Downloads the attached image, submits it to the Sightengine AI-detection API, and reports the resulting AI-likelihood percentage.</summary>
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

            var (status, percentage) = AIMessageService.ParseImageDetectionResult(body);

            if (status != "success" || percentage is not { } rate)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "AI Detection", "The detection request failed.", Username).Build());
                return;
            }

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

    /// <summary>Posts a random Spotify track matching the given mood, with a reroll button attached.</summary>
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

    /// <summary>Re-rolls the mood track in-place on the same message when the Reroll button is pressed.</summary>
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

    /// <summary>Fetches a track for the mood and builds the resulting embed+button pair, or a "no results" embed with no reroll button if nothing matched.</summary>
    private async Task<(Embed embed, MessageComponent components)> BuildMoodResponseAsync(string mood)
    {
        var track = await _spotifyService.GetRandomTrackAsync(mood);

        if (track is null)
        {
            return (
                _embed.BuildSimpleEmbed(
                    "❌  No Results", $"Spotify returned nothing for **{EscapeMd(mood)}**. Try a different mood!",
                    ColourError, footer: $"Requested by {Username}", footerIconUrl: AvatarUrl).Build(),
                new ComponentBuilder().Build());
        }

        return (BuildSpotifyEmbed(mood, track).Build(), BuildRerollButton(mood));
    }

    /// <summary>Builds the track-details embed (artist/album/duration/popularity, plus a preview link if one exists).</summary>
    private EmbedBuilder BuildSpotifyEmbed(string mood, SpotifyTrack t)
    {
        var duration  = TimeSpan.FromMilliseconds(t.DurationMs);
        string expl   = t.Explicit ? " 🅴" : "";

        var embed = _embed.BuildSimpleEmbed(
            $"{t.Name}{expl}", $"A track picked for your **{EscapeMd(mood)}** mood.", ColourSpotify,
            footer: $"Powered by Spotify  •  Requested by {Username}", footerIconUrl: AvatarUrl,
            fields: [("Artist", t.Artist, true),
                     ("Album", $"[{t.Album}]({t.AlbumUrl})", true),
                     ("Duration", $"`{duration:mm\\:ss}`", true),
                     ("Popularity", $"{SpotifyStars(t.Popularity)} `{t.Popularity}/100`", true)])
            .WithUrl(t.Url).WithThumbnailUrl(t.ArtworkUrl);

        if (!string.IsNullOrEmpty(t.PreviewUrl))
            embed.AddField("30s Preview", $"[▶ Listen]({t.PreviewUrl})", inline: true);

        return embed;
    }

    /// <summary>Builds the single Reroll button, carrying the mood string in its custom ID.</summary>
    private static MessageComponent BuildRerollButton(string mood) =>
        new ComponentBuilder()
            .WithButton("🎲  Reroll", $"{BtnRerollPrefix}{mood}", ButtonStyle.Success)
            .Build();

    /// <summary>Renders a 0-100 popularity score as a 5-star rating.</summary>
    private static string SpotifyStars(int popularity)
    {
        int stars = (int)Math.Round(popularity / 20.0);
        return string.Create(5, stars, static (span, s) =>
        {
            span.Fill('☆');
            span[..s].Fill('★');
        });
    }

    /// <summary>Escapes Discord markdown special characters so user-supplied text (e.g. the mood string) can't break embed formatting.</summary>
    private static string EscapeMd(string s) =>
        s.Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`").Replace("~", "\\~");
}
