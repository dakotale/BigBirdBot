


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddPlayerConnected] 
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@VoiceChannelID bigint,
	@TextChannelID bigint,
	@CreatedBy varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	IF NOT EXISTS (SELECT 1 FROM PlayerConnected WHERE ServerUID = @ServerID AND VoiceChannelID = @VoiceChannelID AND TextChannelID = @TextChannelID)
	BEGIN
		-- Insert statements for procedure here
		INSERT INTO [dbo].[PlayerConnected]
			   ([ServerUID]
			   ,[VoiceChannelID]
			   ,[TextChannelID]
			   ,[CreatedOn]
			   ,[CreatedBy])
		 VALUES
			   (@ServerID
			   ,@VoiceChannelID
			   ,@TextChannelID
			   ,GETDATE()
			   ,@CreatedBy)

		UPDATE Servers
		SET IsPlayerConnected = 1
		WHERE ServerUID = @ServerID
	END
    
END
