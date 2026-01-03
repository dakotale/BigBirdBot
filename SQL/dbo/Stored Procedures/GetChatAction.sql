
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
		@Keyword = ck.Keyword,
		@ChatKeywordID = ck.ChatKeywordID
	FROM ChatKeyword ck
	JOIN ThirstMap tm ON ck.ChatKeywordID = tm.ChatKeywordID
	JOIN MessageWords mw ON LOWER(ck.Keyword) = mw.Word
	WHERE ck.ServerID = @ServerID
	  AND ck.IsActive = 1
	ORDER BY NEWID();

	-- If a keyword was matched
	IF @Keyword IS NOT NULL
	BEGIN
		SELECT TOP 1
			ID,
			FilePath AS ChatAction,
			NSFW
		FROM ChatKeywordMultiple
		WHERE ChatKeyword = @Keyword
		ORDER BY NEWID();
	END
	ELSE
	BEGIN
		DECLARE @Prefix VARCHAR(10) = (
			SELECT Prefix FROM Servers WHERE ServerUID = @ServerID
		);

		SELECT TOP 1
			s.ServerUID,
			ck.ChatKeywordID,
			ck.Keyword,
			CASE LOWER(@NormalizedMessage)
				WHEN 'cat' THEN dbo.GetCatFromAPI()
				WHEN 'dog' THEN dbo.GetDogFromAPI()
				WHEN 'fox' THEN dbo.GetFoxFromAPI()
				ELSE REPLACE(ca.ChatAction, '\r\n', CHAR(13) + CHAR(10))
			END AS ChatAction
		FROM Servers s
		JOIN ChatKeyword ck ON s.ServerUID = ck.ServerID
		JOIN ChatAction ca ON ck.ChatKeywordID = ca.ChatKeywordID
		WHERE s.ServerUID = @ServerID
		  AND @NormalizedMessage NOT LIKE LOWER(@Prefix) + '%'
		  AND ck.IsActive = 1
		  AND (
			  @NormalizedMessage = LOWER(ck.Keyword)
			  OR ' ' + @NormalizedMessage + ' ' LIKE '% ' + LOWER(ck.Keyword) + ' %'
		  )
		ORDER BY NEWID();
	END
END
