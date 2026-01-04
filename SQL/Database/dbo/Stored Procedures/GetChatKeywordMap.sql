

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetChatKeywordMap]
	-- Add the parameters for the stored procedure here
	@AddKeyword varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT TOP(1)
		ck.ChatKeyword	[Keyword]
	FROM
		ChatKeywordMap ckm
		JOIN ChatKeyword ck ON ckm.Keyword = ck.ChatKeyword
	WHERE
		AddKeyword = @AddKeyword

END
