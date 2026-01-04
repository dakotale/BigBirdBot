







-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateBrokenEmbed]
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
	SET FixEmbed = ~FixEmbed
	WHERE ServerUID = @ServerID

	SELECT
		FixEmbed
		,CASE 
			WHEN FixEmbed = 1
			THEN 'The bot will now embed Twitter, Reddit, and Bluesky links.'
			ELSE 'The bot will no longer embed Twitter, Reddit, and Bluesky links.'
		END AS [Result]
	FROM
		Servers
	WHERE
		ServerUID = @ServerID

END
