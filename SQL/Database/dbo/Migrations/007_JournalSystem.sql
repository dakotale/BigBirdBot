-- ============================================================
-- Migration 007: Journal System
-- ============================================================

-- ── Tables ───────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalSubscriptions')
CREATE TABLE dbo.JournalSubscriptions
(
    UserID              VARCHAR(50)  NOT NULL PRIMARY KEY,
    DailyTimeUtc        TIME(0)      NOT NULL,
    DailyTimeDisplay    VARCHAR(30)  NOT NULL,
    SubscribedAt        DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    LastReminderSentAt  DATETIME2    NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JournalEntries')
CREATE TABLE dbo.JournalEntries
(
    EntryID    INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    UserID     VARCHAR(50)  NOT NULL,
    EntryDate  DATE         NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    LoggedAt   DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UX_JournalEntries_UserDate UNIQUE (UserID, EntryDate)
);
GO

-- ── Stored Procedures ────────────────────────────────────────

-- Subscribe / update subscription
IF OBJECT_ID('dbo.UpsertJournalSubscription', 'P') IS NOT NULL DROP PROCEDURE dbo.UpsertJournalSubscription;
GO
CREATE PROCEDURE dbo.UpsertJournalSubscription
    @UserID           VARCHAR(50),
    @DailyTimeUtc     VARCHAR(10),  -- "HH:mm:ss"
    @DailyTimeDisplay VARCHAR(30)   -- "9:00 AM (UTC-5)"
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.JournalSubscriptions AS t
    USING (SELECT @UserID AS UserID) AS s ON t.UserID = s.UserID
    WHEN MATCHED THEN
        UPDATE SET
            DailyTimeUtc       = CAST(@DailyTimeUtc AS TIME),
            DailyTimeDisplay   = @DailyTimeDisplay,
            SubscribedAt       = SYSUTCDATETIME(),
            LastReminderSentAt = NULL
    WHEN NOT MATCHED THEN
        INSERT (UserID, DailyTimeUtc, DailyTimeDisplay)
        VALUES (@UserID, CAST(@DailyTimeUtc AS TIME), @DailyTimeDisplay);
END;
GO

-- Unsubscribe
IF OBJECT_ID('dbo.DeleteJournalSubscription', 'P') IS NOT NULL DROP PROCEDURE dbo.DeleteJournalSubscription;
GO
CREATE PROCEDURE dbo.DeleteJournalSubscription
    @UserID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.JournalSubscriptions WHERE UserID = @UserID;
END;
GO

-- Atomically fetch and mark due daily reminders
IF OBJECT_ID('dbo.GetDueJournalReminders', 'P') IS NOT NULL DROP PROCEDURE dbo.GetDueJournalReminders;
GO
CREATE PROCEDURE dbo.GetDueJournalReminders
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.JournalSubscriptions
    SET LastReminderSentAt = SYSUTCDATETIME()
    OUTPUT INSERTED.UserID
    WHERE CAST(GETUTCDATE() AS TIME) >= DailyTimeUtc
      AND (LastReminderSentAt IS NULL
           OR CAST(LastReminderSentAt AS DATE) < CAST(GETUTCDATE() AS DATE));
END;
GO

-- Log a journal entry and return streak
IF OBJECT_ID('dbo.LogJournalEntry', 'P') IS NOT NULL DROP PROCEDURE dbo.LogJournalEntry;
GO
CREATE PROCEDURE dbo.LogJournalEntry
    @UserID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);

    IF EXISTS (SELECT 1 FROM dbo.JournalEntries WHERE UserID = @UserID AND EntryDate = @Today)
    BEGIN
        SELECT
            1 AS AlreadyLogged,
            (
                SELECT COUNT(*) FROM (
                    SELECT EntryDate,
                           DATEDIFF(DAY, EntryDate, @Today) - ROW_NUMBER() OVER (ORDER BY EntryDate DESC) AS Grp
                    FROM dbo.JournalEntries
                    WHERE UserID = @UserID AND EntryDate <= @Today
                ) x WHERE Grp = 0
            ) AS Streak;
        RETURN;
    END;

    INSERT INTO dbo.JournalEntries (UserID) VALUES (@UserID);

    SELECT
        0 AS AlreadyLogged,
        (
            SELECT COUNT(*) FROM (
                SELECT EntryDate,
                       DATEDIFF(DAY, EntryDate, @Today) - ROW_NUMBER() OVER (ORDER BY EntryDate DESC) AS Grp
                FROM dbo.JournalEntries
                WHERE UserID = @UserID AND EntryDate <= @Today
            ) x WHERE Grp = 0
        ) AS Streak;
END;
GO

-- Get subscription + streak summary
IF OBJECT_ID('dbo.GetJournalStatus', 'P') IS NOT NULL DROP PROCEDURE dbo.GetJournalStatus;
GO
CREATE PROCEDURE dbo.GetJournalStatus
    @UserID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Today DATE = CAST(GETUTCDATE() AS DATE);

    SELECT
        CAST(CASE WHEN s.UserID IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasSubscription,
        ISNULL(s.DailyTimeDisplay, '') AS DailyTimeDisplay,
        (SELECT COUNT(*) FROM dbo.JournalEntries WHERE UserID = @UserID) AS TotalEntries,
        ISNULL((
            SELECT COUNT(*) FROM (
                SELECT EntryDate,
                       DATEDIFF(DAY, EntryDate, @Today) - ROW_NUMBER() OVER (ORDER BY EntryDate DESC) AS Grp
                FROM dbo.JournalEntries
                WHERE UserID = @UserID AND EntryDate <= @Today
            ) x WHERE Grp = 0
        ), 0) AS Streak
    FROM (SELECT @UserID AS UserID) u
    LEFT JOIN dbo.JournalSubscriptions s ON s.UserID = u.UserID;
END;
GO
