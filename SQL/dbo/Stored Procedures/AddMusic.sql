-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddMusic]
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@VideoID nvarchar(100),
	@Author nvarchar(100),
	@Title nvarchar(200),
	@URL nvarchar(500),
	@CreatedBy nvarchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO Music
	VALUES (@ServerID, @VideoID, @Author, @Title, @URL, GETDATE(), COALESCE(@CreatedBy, ''))

	DECLARE @MusicID int = (SELECT MAX(MusicID) FROM Music)
	DECLARE @VoiceChannelID bigint = (SELECT VoiceChannelID FROM PlayerConnected WHERE ServerUID = @ServerID)
	DECLARE @TextChannelID bigint = (SELECT TextChannelID FROM PlayerConnected WHERE ServerUID = @ServerID)

	INSERT INTO MusicQueue
	VALUES (@MusicID, @ServerID, @VoiceChannelID, @TextChannelID, @URL, GETDATE(), COALESCE(@CreatedBy, ''))
END
