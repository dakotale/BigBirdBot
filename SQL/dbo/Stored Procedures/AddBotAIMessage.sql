-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddBotAIMessage]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@ServerUID varchar(50),
	@ChannelID varchar(50) = null,
	@ChatRole varchar(10),
	@ChatMessage nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	INSERT INTO [dbo].[BotAIMessage]
			([UserID]
			,[ServerUID]
			,[ChannelID]
			,[ChatRole]
			,[ChatMessage]
			,[CreatedOn])
		VALUES
			(@UserID
			,@ServerUID
			,@ChannelID
			,@ChatRole
			,@ChatMessage
			,GETDATE())
END
