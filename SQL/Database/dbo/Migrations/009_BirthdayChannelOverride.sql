-- ============================================================
-- Migration 009: Birthday Channel Override
-- ============================================================

-- ── Tables ───────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Birthday') AND name = 'BirthdayChannel')
ALTER TABLE dbo.Birthday ADD BirthdayChannel NVARCHAR(100) NULL;
GO

-- ── Stored Procedures ────────────────────────────────────────

IF OBJECT_ID('dbo.AddBirthday', 'P') IS NOT NULL DROP PROCEDURE dbo.AddBirthday;
GO
CREATE PROCEDURE dbo.AddBirthday
    @BirthdayDate    DATETIME,
    @BirthdayUser    NVARCHAR(100),
    @BirthdayGuild   NVARCHAR(100),
    @BirthdayChannel NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- One row per year for the next 9 years so the exact-date match in
    -- GetTodaysBirthdays fires once annually without any wraparound logic.
    INSERT INTO dbo.Birthday (BirthdayDate, BirthdayUser, BirthdayGuild, BirthdayChannel)
    SELECT DATEADD(YEAR, n, @BirthdayDate), @BirthdayUser, @BirthdayGuild, @BirthdayChannel
    FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8)) AS Years(n);
END;
GO

-- Atomically fetch and mark today's birthdays so each row only fires once
IF OBJECT_ID('dbo.GetTodaysBirthdays', 'P') IS NOT NULL DROP PROCEDURE dbo.GetTodaysBirthdays;
GO
CREATE PROCEDURE dbo.GetTodaysBirthdays
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Birthday
    SET    Sent = 1
    OUTPUT INSERTED.BirthdayID,
           INSERTED.BirthdayUser,
           INSERTED.BirthdayGuild,
           INSERTED.BirthdayChannel
    WHERE  Sent = 0
      AND  CAST(BirthdayDate AS DATE) = CAST(GETDATE() AS DATE);
END;
GO
