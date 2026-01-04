

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddTriviaMessage]
	@TriviaMessageID bigint,
	@CorrectAnswer nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO TriviaMessage
	VALUES (@TriviaMessageID, @CorrectAnswer, GETDATE())
END
