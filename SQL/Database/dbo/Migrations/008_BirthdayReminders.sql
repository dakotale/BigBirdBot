-- ============================================================
-- Migration 008: Birthday Reminders
-- ============================================================

-- ── Tables ───────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Birthday') AND name = 'Sent')
ALTER TABLE dbo.Birthday ADD Sent BIT NOT NULL DEFAULT (0);
GO

-- ── Stored Procedures ────────────────────────────────────────

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
           INSERTED.BirthdayGuild
    WHERE  Sent = 0
      AND  CAST(BirthdayDate AS DATE) = CAST(GETDATE() AS DATE);
END;
GO
