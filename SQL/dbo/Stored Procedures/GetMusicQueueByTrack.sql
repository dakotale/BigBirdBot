




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetMusicQueueByTrack]
	-- Add the parameters for the stored procedure here
	@URL nvarchar(500),
	@TextChannelID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
SELECT
	MusicID
	,MusicQueueID
	,ServerUID
	,VoiceChannelID
	,TextChannelID
	,URL
	,CreatedBy
FROM
	MusicQueue
WHERE
	TextChannelID = @TextChannelID
	AND URL = @URL
ORDER BY
	CreatedOn

END
