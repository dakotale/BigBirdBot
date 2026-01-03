-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[CheckIfThirstURLExists]
	-- Add the parameters for the stored procedure here
	@FilePath varchar(max),
	@TableName varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT 1 FROM ChatKeywordMultiple WHERE convert(varchar, FilePath) = @FilePath AND ChatKeyword = @TableName
END
