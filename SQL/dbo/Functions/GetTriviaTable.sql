
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[GetTriviaTable]
(
    @Token char(64)
)
RETURNS @TriviaDetails TABLE
(
    Category nvarchar(max) NOT NULL,
    Difficulty nvarchar(max) NOT NULL,
    Question nvarchar(max) NOT NULL,
    CorrectAnswer nvarchar(max) NOT NULL,
    FirstIncorrect nvarchar(max) NULL,
    SecondIncorrect nvarchar(max) NULL,
    ThirdIncorrect nvarchar(max) NULL
)
AS
BEGIN
    DECLARE @URL NVARCHAR(MAX) = 'https://opentdb.com/api.php?amount=1&multiple&token=aa9bf7136c024b817d335168bf7e2b77c76b3d40bcdaba5185d87b061d6a9342';--'https://opentdb.com/api.php?amount=1&type=multiple&token=' + @Token;
    DECLARE @Object INT = NULL;
    DECLARE @ResponseText NVARCHAR(MAX) = NULL;

    -- Create XMLHTTP object
    EXEC sp_OACreate 'MSXML2.XMLHTTP', @Object OUT;
    IF @Object IS NULL
    BEGIN
        RETURN; -- Fail gracefully if object creation failed
    END

    -- Open HTTP request synchronously
    EXEC sp_OAMethod @Object, 'open', NULL, 'GET', @URL, false;

    -- Send HTTP request
    EXEC sp_OAMethod @Object, 'send';

    -- Get the response text (use NVARCHAR for unicode support)
    EXEC sp_OAMethod @Object, 'responseText', @ResponseText OUTPUT;

    -- Check if response is empty
    IF (@ResponseText IS NULL OR LEN(@ResponseText) = 0)
    BEGIN
        RETURN; -- No data, just return empty table
    END

    -- Parse JSON and insert into output table
    INSERT INTO @TriviaDetails(Category, Difficulty, Question, CorrectAnswer, FirstIncorrect, SecondIncorrect, ThirdIncorrect)
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

    IF @Object IS NOT NULL
    BEGIN
        EXEC sp_OADestroy @Object;
    END

    RETURN;
END;

