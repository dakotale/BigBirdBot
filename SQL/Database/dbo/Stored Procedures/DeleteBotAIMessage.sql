
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteBotAIMessage]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@ServerUID varchar(50),
	@ChannelID varchar(50) 
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DELETE FROM [DiscordBot].[dbo].[BotAIMessage]
	WHERE UserID = @UserID AND (ServerUID = @ServerUID OR ChannelID = @ChannelID)
END
