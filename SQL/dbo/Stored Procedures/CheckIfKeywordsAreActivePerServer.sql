-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[CheckIfKeywordsAreActivePerServer] 
	-- Add the parameters for the stored procedure here
	@ServerUID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
		COUNT(ck.ChatKeywordID)	[TotalActive]
	FROM
		ChatKeyword ck
		JOIN ChatAction ca ON ck.ChatKeywordID = ca.ChatKeywordID
	WHERE
		ck.ServerID = @ServerUID
		AND IsActive = 1
		AND IsMassDisabled = 0
END
