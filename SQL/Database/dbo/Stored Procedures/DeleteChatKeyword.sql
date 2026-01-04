
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteChatKeyword]
	-- Add the parameters for the stored procedure here
	@Keyword int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @ChatKeywordID INT = (SELECT ck.ID FROM ChatKeyword ck WHERE ck.ChatKeyword = @Keyword)

    -- Insert statements for procedure here
	DELETE FROM ChatKeywordMap 
	WHERE AddKeyword = 'add' + @Keyword

	DELETE FROM ChatKeyword
	WHERE ChatKeyword = @Keyword

	DELETE FROM UsersScheduledKeyword
	WHERE ChatKeyword = @Keyword
END
