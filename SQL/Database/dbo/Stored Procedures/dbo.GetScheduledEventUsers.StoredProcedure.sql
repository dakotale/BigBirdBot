USE [DiscordBot]
GO
/****** Object:  StoredProcedure [dbo].[GetScheduledEventUsers] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Description: Admin/owner-only listing of every user with a scheduled
-- keyword delivery, across all users (no @UserID filter). Used by
-- OwnerCommands.HandleServerList ("schedulelist"). Column names match
-- what that command reads: Username, ScheduledEventTable, EventDateTime.
-- =============================================
CREATE PROCEDURE [dbo].[GetScheduledEventUsers]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		u.Username,
		usk.ChatKeyword       [ScheduledEventTable],
		usk.ScheduledDateTime [EventDateTime]
	FROM
		UsersScheduledKeyword usk
	JOIN
		Users u ON u.UserID = usk.UserID
	ORDER BY
		usk.ScheduledDateTime
END
GO
