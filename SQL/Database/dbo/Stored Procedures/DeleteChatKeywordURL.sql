


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteChatKeywordURL]
	-- Add the parameters for the stored procedure here
	@FilePath nvarchar(max),
	@Keyword varchar(50)=''
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DELETE FROM ChatKeyword WHERE FilePath = @FilePath AND ChatKeyword = @Keyword
END
