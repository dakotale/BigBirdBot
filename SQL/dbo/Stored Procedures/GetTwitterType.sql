-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE GetTwitterType
	-- Add the parameters for the stored procedure here
	@json nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		tweetUrl,
		COALESCE(videoUrl, '')	[videoUrl]
	FROM
		OPENJSON(@json, '$.tweet')
		WITH
		(
			tweetUrl varchar(max) '$.url',
			videoUrl varchar(max) '$.media.videos[0].url'
		)
END
