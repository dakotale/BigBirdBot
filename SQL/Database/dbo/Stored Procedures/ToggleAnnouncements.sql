CREATE PROCEDURE [dbo].[ToggleAnnouncements]
    @ServerUID  BIGINT,
    @ChannelID  BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Servers]
    SET    [AnnouncementsEnabled] = ~[AnnouncementsEnabled],
           [DefaultChannelID]     = CASE WHEN ~[AnnouncementsEnabled] = 1 THEN @ChannelID ELSE [DefaultChannelID] END
    WHERE  [ServerUID] = @ServerUID;

    SELECT
        [AnnouncementsEnabled],
        CASE
            WHEN [AnnouncementsEnabled] = 1
            THEN 'Announcements enabled. Timed events (word puzzles, jackpot results) will be posted in this channel.'
            ELSE 'Announcements disabled. The bot will no longer post timed events in this server.'
        END AS [Result]
    FROM [dbo].[Servers]
    WHERE [ServerUID] = @ServerUID;
END
