


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateChatAction]
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@Keyword varchar(50),
	@Action nvarchar(2000)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DECLARE @ChatKeywordID int = (SELECT ChatKeywordID FROM ChatKeyword WHERE ServerID = @ServerID AND Keyword = @Keyword)

	UPDATE ChatKeyword
	SET IsActive = 1
	WHERE ChatKeywordID = @ChatKeywordID

	if (LEN(@Action) > 0)
	BEGIN
		UPDATE ChatAction
		SET ChatAction = @Action
		WHERE ChatKeywordID = @ChatKeywordID
	END


END
