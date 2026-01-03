

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddChatKeywordAction]
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@Keyword varchar(50),
	@CreatedBy varchar(50),
	@Action nvarchar(2000)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @DoesExist int = (SELECT 1 FROM ChatKeyword WHERE Keyword = @Keyword AND ServerID = @ServerID)

	if (@DoesExist = 1)
		RETURN

    -- Insert statements for procedure here
	INSERT INTO ChatKeyword
	VALUES (@ServerID, @Keyword, GETDATE(), 1, 0, @CreatedBy)

	DECLARE @ChatKeywordID int = (SELECT MAX(ChatKeywordID) FROM ChatKeyword)

	INSERT INTO ChatAction
	VALUES (@ChatKeywordID, (CASE WHEN @Action = '''' THEN '' ELSE @Action END), GETDATE(), @CreatedBy)

END
