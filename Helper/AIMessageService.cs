using System.Text.Json;
using DiscordBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Helper;

/// <summary>
/// EF Core access for <c>/chat</c> conversation history and the <c>/detectaibyattachment</c>
/// result parser. Replaces <c>AddBotAIMessage</c>, <c>DeleteBotAIMessage</c>,
/// <c>GetBotAIMessage</c>, and <c>GetAIJSONImageReturn</c>.
/// </summary>
public sealed class AIMessageService(IDbContextFactory<BigBirdContext> contextFactory)
{
    /// <summary>Appends one turn (user or assistant) to a conversation's history. Replaces <c>AddBotAIMessage</c>.</summary>
    public async Task AddMessageAsync(string userId, string serverUid, string? channelId, string chatRole, string chatMessage)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        db.BotAiMessages.Add(new BotAiMessage
        {
            UserId = userId,
            ServerUid = serverUid,
            ChannelId = channelId,
            ChatRole = chatRole,
            ChatMessage = chatMessage,
            CreatedOn = DateTime.Now
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Clears a user's conversation history for a server or channel (matches either — same
    /// OR condition as the original). Replaces <c>DeleteBotAIMessage</c>.
    /// </summary>
    public async Task DeleteHistoryAsync(string userId, string serverUid, string channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        await db.BotAiMessages
            .Where(m => m.UserId == userId && (m.ServerUid == serverUid || m.ChannelId == channelId))
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// A user's conversation history for a server or channel (matches either), oldest first.
    /// Replaces <c>GetBotAIMessage</c>.
    /// </summary>
    public async Task<IReadOnlyList<(string Role, string Text)>> GetHistoryAsync(string userId, string serverUid, string channelId)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var rows = await db.BotAiMessages
            .Where(m => m.UserId == userId && (m.ServerUid == serverUid || m.ChannelId == channelId))
            .OrderBy(m => m.BotAiMessageId)
            .Select(m => new { m.ChatRole, m.ChatMessage })
            .ToListAsync();

        return rows.Select(r => (r.ChatRole, r.ChatMessage)).ToList();
    }

    /// <summary>
    /// Extracts the Sightengine AI-detection result (status + AI-likelihood percentage) from
    /// its raw JSON response. Replaces <c>GetAIJSONImageReturn</c> — that procedure only ever
    /// parsed its <c>@json</c> parameter via <c>OPENJSON</c> with no table involved, so this
    /// is done natively in C# instead of round-tripping to the database for pure computation.
    /// Returns <c>(null, null)</c> for unparsable JSON or a missing field, matching
    /// <c>OPENJSON ... WITH (...)</c>'s behaviour of returning null columns rather than
    /// erroring on a missing path.
    /// </summary>
    public static (string? Status, double? PercentageChance) ParseImageDetectionResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? status = root.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString()
                : null;

            double? percentage = null;
            if (root.TryGetProperty("type", out var typeEl) &&
                typeEl.TryGetProperty("ai_generated", out var resultEl))
            {
                string? resultText = resultEl.ValueKind == JsonValueKind.String
                    ? resultEl.GetString()
                    : resultEl.ToString();

                if (double.TryParse(resultText, out double parsed))
                    percentage = parsed * 100.0;
            }

            return (status, percentage);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
