





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetKeywordNSFW]
	-- Add the parameters for the stored procedure here
	@Message nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT TOP(1) NSFW FROM ChatKeywordMultiple WHERE FilePath LIKE '%' + @Message + '%'
END
