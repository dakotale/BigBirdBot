
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[CheckKeywordExistsThirstMapByServer]
	-- Add the parameters for the stored procedure here
	@Keyword varchar(50),
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		tm.AddKeyword
		,tm.ChatKeywordID
		,'' [TableName]
		,ck.ServerID
	FROM ThirstMap tm
	JOIN ChatKeyword ck ON tm.ChatKeywordID = ck.ChatKeywordID
	WHERE ck.Keyword = @Keyword AND ck.ServerID = @ServerID
END
