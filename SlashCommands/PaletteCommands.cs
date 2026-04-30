using Discord;
using Discord.Interactions;
using DiscordBot.Constants;
using DiscordBot.Helper;
using SkiaSharp;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DiscordBot.SlashCommands
{
    public class PaletteCommands : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly EmbedHelper _embed = new();
        private readonly IHttpClientFactory _httpFactory;

        private const int SwatchWidth  = 200;
        private const int SwatchHeight = 160;
        private const int LabelHeight  = 50;
        private const int ImageWidth   = SwatchWidth * 5;
        private const int ImageHeight  = SwatchHeight + LabelHeight;

        public PaletteCommands(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        [SlashCommand("palette", "Generate a color palette using AI.")]
        [CommandContextType(InteractionContextType.Guild)]
        public async Task HandlePaletteAsync(
            [Summary("prompt", "Describe the mood, theme, or style for the palette.")] string prompt)
        {
            await DeferAsync();

            List<(string Hex, string Name)> colors;
            try
            {
                colors = await GetPaletteFromAnthropicAsync(prompt);
            }
            catch (Exception ex)
            {
                await FollowupAsync(embed: _embed.BuildErrorEmbed(
                    "Palette", $"Could not generate palette: {ex.Message}", Context.User.Username).Build(),
                    ephemeral: true);
                return;
            }

            using var stream = RenderPalette(colors);

            var embed = new EmbedBuilder()
                .WithTitle($"Color Palette — {prompt}")
                .WithColor(EmbedColors.Purple)
                .WithImageUrl("attachment://palette.png")
                .WithFooter(Context.User.Username)
                .WithCurrentTimestamp()
                .Build();

            await FollowupWithFileAsync(stream, "palette.png", embed: embed);
        }

        private async Task<List<(string Hex, string Name)>> GetPaletteFromAnthropicAsync(string prompt)
        {
            var client = _httpFactory.CreateClient();

            var body = JsonSerializer.Serialize(new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 512,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $"Generate exactly 5 colors for a palette described as: \"{prompt}\". " +
                                  "Respond ONLY with a JSON array of objects, each with \"hex\" (e.g. \"#A3C4BC\") " +
                                  "and \"name\" (short descriptive name). No markdown, no explanation."
                    }
                }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", Constants.Constants.anthropicApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Anthropic API error {(int)response.StatusCode}: {responseBody}");

            using var doc = JsonDocument.Parse(responseBody);
            string text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString()!;

            int start = text.IndexOf('[');
            int end   = text.LastIndexOf(']');
            if (start < 0 || end < 0)
                throw new InvalidOperationException("No JSON array found in Anthropic response.");

            using var arr = JsonDocument.Parse(text[start..(end + 1)]);
            var result = new List<(string, string)>();
            foreach (var el in arr.RootElement.EnumerateArray())
            {
                string hex  = el.GetProperty("hex").GetString()!.Trim();
                string name = el.GetProperty("name").GetString()!.Trim();
                result.Add((hex, name));
            }

            if (result.Count == 0)
                throw new InvalidOperationException("Palette response contained no colors.");

            return result;
        }

        private static MemoryStream RenderPalette(List<(string Hex, string Name)> colors)
        {
            var info   = new SKImageInfo(ImageWidth, ImageHeight);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White);

            using var swatchPaint = new SKPaint { IsAntialias = false };
            using var textPaint   = new SKPaint { IsAntialias = true, TextSize = 13f, Typeface = SKTypeface.Default };

            for (int i = 0; i < Math.Min(colors.Count, 5); i++)
            {
                var (hex, name) = colors[i];
                int x = i * SwatchWidth;

                if (!SKColor.TryParse(hex, out var color))
                    color = SKColors.Gray;

                // Draw swatch
                swatchPaint.Color = color;
                canvas.DrawRect(SKRect.Create(x, 0, SwatchWidth, SwatchHeight), swatchPaint);

                // Hex label inside swatch — contrast color
                SKColor textColor = Luminance(color) > 0.5f ? SKColors.Black : SKColors.White;
                textPaint.Color = textColor;

                float hexWidth = textPaint.MeasureText(hex);
                canvas.DrawText(hex, x + (SwatchWidth - hexWidth) / 2f, SwatchHeight - 12f, textPaint);

                // Color name below swatch
                textPaint.Color = SKColors.Black;
                float nameWidth = textPaint.MeasureText(name);
                float nameX = x + (SwatchWidth - nameWidth) / 2f;
                canvas.DrawText(name, nameX, SwatchHeight + 30f, textPaint);
            }

            var ms = new MemoryStream();
            using var image = surface.Snapshot();
            using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
            data.SaveTo(ms);
            ms.Position = 0;
            return ms;
        }

        private static float Luminance(SKColor c) =>
            (0.299f * c.Red + 0.587f * c.Green + 0.114f * c.Blue) / 255f;
    }
}
