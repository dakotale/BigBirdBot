-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteThirstCommand]
	-- Add the parameters for the stored procedure here
	@ChatKeywordID int,
	@TableName varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DELETE FROM ChatKeyword
	WHERE ChatKeywordID = @ChatKeywordID

	DELETE FROM ChatAction
	WHERE ChatKeywordID = @ChatKeywordID

	DELETE FROM ThirstMap
	WHERE ChatKeywordID = @ChatKeywordID

	DELETE FROM Roles
	WHERE RoleName = (SELECT Keyword FROM ChatKeyword WHERE ChatKeywordID = @ChatKeywordID)
END
