

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetTrivia]
	@ResponseText nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
        Category,
        Difficulty,
        Question,
        CorrectAnswer,
        FirstIncorrect,
        SecondIncorrect,
        ThirdIncorrect
    FROM OPENJSON(@ResponseText)
    WITH
    (
        Category nvarchar(max) '$.results[0].category',
        Difficulty nvarchar(max) '$.results[0].difficulty',
        Question nvarchar(max) '$.results[0].question',
        CorrectAnswer nvarchar(max) '$.results[0].correct_answer',
        FirstIncorrect nvarchar(max) '$.results[0].incorrect_answers[0]',
        SecondIncorrect nvarchar(max) '$.results[0].incorrect_answers[1]',
        ThirdIncorrect nvarchar(max) '$.results[0].incorrect_answers[2]'
    );
END
