-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetThirstMapByServerID]
	-- Add the parameters for the stored procedure here
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT DISTINCT
		ck.Keyword	[TableList]
	FROM
		ThirstMap tm
		JOIN ChatKeyword ck ON tm.ChatKeywordID = ck.ChatKeywordID
		JOIN [Servers] s ON ck.ServerID = s.ServerUID
	WHERE
		s.ServerUID = @ServerID
	ORDER BY
		TableList
END
