CREATE PROCEDURE [dbo].[ToggleAnnouncements]
    @ServerUID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Servers]
    SET    [AnnouncementsEnabled] = ~[AnnouncementsEnabled]
    WHERE  [ServerUID] = @ServerUID;

    SELECT
        [AnnouncementsEnabled],
        CASE
            WHEN [AnnouncementsEnabled] = 1
            THEN 'Announcements enabled. The bot will post timed events (word puzzles, jackpot results) in the default channel.'
            ELSE 'Announcements disabled. The bot will no longer post timed events in this server.'
        END AS [Result]
    FROM [dbo].[Servers]
    WHERE [ServerUID] = @ServerUID;
END
