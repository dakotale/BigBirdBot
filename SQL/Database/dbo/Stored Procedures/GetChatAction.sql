

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetChatAction]
    @ServerID bigint,
    @Message varchar(2000)
AS
BEGIN
    SET NOCOUNT ON;

    -- Normalize message to lowercase
	-- Normalize the input message
	DECLARE @NormalizedMessage VARCHAR(2000) = LOWER(@Message);
	DECLARE @Keyword VARCHAR(50);
	DECLARE @ChatKeywordID INT;

	-- Try exact word match using STRING_SPLIT (SQL Server 2016+)
	;WITH MessageWords AS (
		SELECT TRIM(value) AS Word
		FROM STRING_SPLIT(@NormalizedMessage, ' ')
	)
	SELECT TOP 1
		@Keyword = ck.ChatKeyword,
		@ChatKeywordID = ck.ID
	FROM ChatKeyword ck
	JOIN ChatKeywordMap ckm ON ck.ChatKeyword = ckm.Keyword
	JOIN MessageWords mw ON LOWER(ck.ChatKeyword) = mw.Word
	WHERE ckm.ServerID = @ServerID
	ORDER BY NEWID();

	-- If a keyword was matched
	IF @Keyword IS NOT NULL
	BEGIN
		SELECT TOP 1
			ID,
			FilePath AS ChatAction,
			NSFW
		FROM ChatKeyword
		WHERE ChatKeyword = @Keyword
		ORDER BY NEWID();
	END
END

