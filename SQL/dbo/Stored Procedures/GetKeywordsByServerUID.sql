
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetKeywordsByServerUID]
	-- Add the parameters for the stored procedure here
	@ServerUID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    SELECT
		ck.Keyword
		,ck.CreatedOn
		,ck.CreatedBy
	FROM
		ChatKeyword ck
		LEFT JOIN ThirstMap tm ON ck.ChatKeywordID = tm.ChatKeywordID
	WHERE
		ck.ServerID = @ServerUID
		AND ck.IsActive = 1
		AND tm.ChatKeywordID IS NULL
	ORDER BY
		ck.Keyword
END
