



-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetMusicQueue]
	-- Add the parameters for the stored procedure here
	@ServerID bigint
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
	ServerUID = @ServerID
ORDER BY
	CreatedOn

END
