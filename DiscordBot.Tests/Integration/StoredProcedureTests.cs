using System.Data;
using System.Data.SqlClient;
using DiscordBot.Constants;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="StoredProcedure"/> that require a live SQL Server
/// database. All tests are skipped automatically when the database is unavailable,
/// so CI passes even without a SQL Server instance.
///
/// To run these tests locally:
///   1. Set the environment variable  discordBotConnStr  to your connection string, OR
///   2. Place a secrets.json file next to the test DLL with key "discordBotConnStr".
/// </summary>
[Collection("Database")]
public class StoredProcedureTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;
    private readonly StoredProcedure _sp = new();

    public StoredProcedureTests(DatabaseFixture db)
    {
        _db = db;
    }

    // ── Connectivity ──────────────────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Database_CanOpenConnection()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        using var conn = new SqlConnection(_db.ConnectionString);
        conn.Open();
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Database_ServerVersionIsReadable()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        using var conn = new SqlConnection(_db.ConnectionString);
        conn.Open();
        Assert.False(string.IsNullOrWhiteSpace(conn.ServerVersion));
    }

    // ── StoredProcedure.Select — no-parameter queries ─────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Select_GetAllStocks_ReturnsDataTable()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        DataTable result = _sp.Select(_db.ConnectionString, "GetAllStocks", []);

        Assert.NotNull(result);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Select_GetAllStocks_HasExpectedColumns()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        DataTable result = _sp.Select(_db.ConnectionString, "GetAllStocks", []);

        string[] expected = ["Ticker", "CompanyName", "Sector", "Price", "PrevPrice",
                             "High24h", "Low24h", "Volatility", "Trend", "LastUpdated"];
        foreach (string col in expected)
            Assert.True(result.Columns.Contains(col), $"Expected column '{col}' not found in result.");
    }

    // ── StoredProcedure.Select — parameterised query ──────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Select_GetCredits_NonExistentUser_ReturnsEmptyTable()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        var parameters = new List<System.Data.SqlClient.SqlParameter>
        {
            new("@UserID",   "0000000000000000"),   // impossible Discord snowflake
            new("@ServerID", "0000000000000000"),
        };

        DataTable result = _sp.Select(_db.ConnectionString, "GetCredits", parameters);

        Assert.NotNull(result);
        Assert.Equal(0, result.Rows.Count);
    }

    // ── StoredProcedure.Select — null parameters ──────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Select_GetAllStocks_NullParameterList_DoesNotThrow()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        // StoredProcedure.Select has a null-guard; verify it doesn't throw.
        DataTable result = _sp.Select(_db.ConnectionString, "GetAllStocks", null!);

        Assert.NotNull(result);
    }

    // ── StoredProcedure.Select — bad SP name ──────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Select_InvalidProcedureName_ThrowsSqlException()
    {
        Skip.If(!_db.IsAvailable, _db.UnavailableReason);

        Assert.ThrowsAny<Exception>(() =>
            _sp.Select(_db.ConnectionString, "NonExistentProcedure_XYZ", []));
    }

    // ── Bad connection string ─────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public void Select_BadConnectionString_ThrowsException()
    {
        // This test does NOT require a live DB — it verifies error propagation.
        const string bad = "Server=localhost\\DOESNOTEXIST;DataBase=NoSuchDB;Connect Timeout=1;TrustServerCertificate=True";

        Assert.ThrowsAny<Exception>(() =>
            _sp.Select(bad, "GetAllStocks", []));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void UpdateCreate_BadConnectionString_ThrowsException()
    {
        const string bad = "Server=localhost\\DOESNOTEXIST;DataBase=NoSuchDB;Connect Timeout=1;TrustServerCertificate=True";

        Assert.ThrowsAny<Exception>(() =>
            _sp.UpdateCreate(bad, "GetAllStocks", []));
    }
}
