





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[MarkKeywordNSFW]
	-- Add the parameters for the stored procedure here
	@Message nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	IF NOT EXISTS (SELECT ChatKeyword FROM ChatKeyword WHERE FilePath LIKE '%' + @Message + '%' AND NSFW = 1)
	BEGIN
		UPDATE ChatKeyword
		SET NSFW = 1
		WHERE FilePath LIKE '%' + @Message + '%'
	END

	SELECT TOP(1) NSFW FROM ChatKeyword WHERE FilePath  LIKE '%' + @Message + '%'
END
