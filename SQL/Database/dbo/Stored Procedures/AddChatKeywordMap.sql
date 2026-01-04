


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddChatKeywordMap]
	@ServerID bigint,
	@Keyword varchar(50),
	@AddKeyword varchar(50),
	@CreatedBy varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	IF NOT EXISTS (SELECT 1 FROM ChatKeywordMap WHERE ServerID = @ServerID AND AddKeyword = @AddKeyword)
	BEGIN
	-- Insert statements for procedure here
	INSERT INTO [dbo].[ChatKeywordMap]
			([AddKeyword]
			,[ServerID]
			,[CreatedOn]
			,[CreatedBy])
		VALUES
			(@AddKeyword
			,@ServerID
			,GETDATE()
			,@CreatedBy)

		SELECT AddKeyword FROM ChatKeywordMap WHERE ServerID = @ServerID AND AddKeyword = @AddKeyword
	END
	ELSE
	BEGIN
		SELECT AddKeyword FROM ChatKeywordMap WHERE ServerID = @ServerID AND AddKeyword = @AddKeyword
	END

END
