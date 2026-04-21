-- =============================================================
-- Migration 005 – Remove hardcoded user exclusion from AddAudit
--
-- The original AddAudit procedure silently skipped inserts when
-- @CreatedBy matched a hardcoded user ID, causing audit entries
-- to never be written for that user.
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

ALTER PROCEDURE [dbo].[AddAudit]
    @Command   varchar(50),
    @CreatedBy varchar(50),
    @ServerID  bigint = null
AS
BEGIN
    SET NOCOUNT ON;

    IF @CreatedBy IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[AuditLog]
               ([Command]
               ,[CreatedOn]
               ,[CreatedBy]
               ,ServerUID)
         VALUES
               (@Command
               ,GETDATE()
               ,@CreatedBy
               ,@ServerID)
    END
END
GO

PRINT 'Migration 005 complete – Hardcoded user exclusion removed from AddAudit.';
GO
