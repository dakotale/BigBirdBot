-- =============================================================
-- Migration 005 – Fix DeleteMusicQueue parameter mismatch
--
-- The procedure was changed to accept @MusicQueueID but the bot
-- still passes @URL, so queue entries were never deleted after a
-- track finished. Reverted to delete by URL (FIFO — removes the
-- oldest matching entry so duplicate-queued tracks work correctly).
--
-- Run once against the live DiscordBot database.
-- =============================================================
USE [DiscordBot]
GO

ALTER PROCEDURE [dbo].[DeleteMusicQueue]
        @URL nvarchar(500)
AS
BEGIN
        SET NOCOUNT ON;

        DELETE FROM [dbo].[MusicQueue]
        WHERE MusicQueueID = (
            SELECT TOP 1 MusicQueueID
            FROM   [dbo].[MusicQueue]
            WHERE  URL = @URL
            ORDER BY MusicQueueID ASC
        );
END
GO
