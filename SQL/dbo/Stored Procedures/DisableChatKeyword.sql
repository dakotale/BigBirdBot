




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DisableChatKeyword]
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@Keyword varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE ChatKeyword
	SET IsActive = 0
	WHERE ServerID = @ServerID AND Keyword = @Keyword

END
