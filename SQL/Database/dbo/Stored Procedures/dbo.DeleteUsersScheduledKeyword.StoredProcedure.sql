USE [DiscordBot]
GO
/****** Object:  StoredProcedure [dbo].[DeleteUsersScheduledKeyword] ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Description: Cancels one scheduled keyword delivery for a user.
-- Used by SlashCommands.Keyword.ScheduleCommands.HandleRemoveAsync
-- ("schedule remove"), called with @UserID and @Keyword.
-- =============================================
CREATE PROCEDURE [dbo].[DeleteUsersScheduledKeyword]
	@UserID  varchar(50),
	@Keyword varchar(50)
AS
BEGIN
	SET NOCOUNT ON;

	DELETE FROM UsersScheduledKeyword
	WHERE UserID = @UserID AND ChatKeyword = @Keyword
END
GO
