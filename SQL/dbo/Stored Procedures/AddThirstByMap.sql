
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddThirstByMap]
	-- Add the parameters for the stored procedure here
	@FilePath nvarchar(max),
	@TableName varchar(50),
	@UserID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SET @FilePath = REPLACE(@FilePath, '''', '')

	INSERT INTO ChatKeywordMultiple
	VALUES (@TableName, TRIM(@FilePath), GETDATE(), 0)
END
