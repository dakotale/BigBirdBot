using Microsoft.Data.SqlClient;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Shared fixture that resolves the test connection string and verifies
/// database availability once per test session.
///
/// Connection string resolution order (same as the bot):
///   1. Environment variable  discordBotConnStr
///   2. discordBotConnStr key in a secrets.json file placed next to the test DLL
///   3. Default localhost Integrated Security string
///
/// Tests decorated with [SkippableFact] will skip automatically when
/// <see cref="IsAvailable"/> is false, keeping CI green without a SQL Server instance.
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public string ConnectionString { get; }
    public bool IsAvailable { get; }
    public string UnavailableReason { get; }

    public DatabaseFixture()
    {
        // Reuse the same resolution logic the bot uses via Constants.
        ConnectionString = DiscordBot.Constants.Constants.discordBotConnStr;

        try
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();
            IsAvailable = true;
            UnavailableReason = string.Empty;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            UnavailableReason = $"Cannot connect to database: {ex.Message}";
        }
    }

    public void Dispose() { }
}
