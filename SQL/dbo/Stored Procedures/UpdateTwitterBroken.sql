





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateTwitterBroken]
(
	@ServerID bigint
)
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE Servers
	SET TwitterBroken = ~TwitterBroken
	WHERE ServerUID = @ServerID

	SELECT
		TwitterBroken
		,CASE 
			WHEN TwitterBroken = 1
			THEN 'Twitter embeds are broken, the bot will now hardcode all Twitter links with FxTwitter.'
			ELSE 'Twitter embeds are no longer broken, the bot will only handle media messages.'
		END AS [Result]
	FROM
		Servers
	WHERE
		ServerUID = @ServerID

END
