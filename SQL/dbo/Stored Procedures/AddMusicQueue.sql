-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddMusicQueue]
	-- Add the parameters for the stored procedure here
	@MusicID int,
	@ServerID bigint,
	@VoiceChannelID bigint,
	@TextChannelID bigint,
	@URL nvarchar(500),
	@CreatedBy varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO [dbo].[MusicQueue]
			   (MusicID
			   ,[ServerUID]
			   ,[VoiceChannelID]
			   ,[TextChannelID]
			   ,[URL]
			   ,[CreatedOn]
			   ,[CreatedBy])
		VALUES
			(@MusicID
			,@ServerID
			,@VoiceChannelID
			,@TextChannelID
			,@URL
			,GETDATE()
			,@CreatedBy)

END
