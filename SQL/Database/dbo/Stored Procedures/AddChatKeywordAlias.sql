USE [DiscordBot]
GO

CREATE PROCEDURE [dbo].[AddChatKeywordAlias]
    @Alias     varchar(50),
    @Keyword   varchar(50),
    @ServerID  bigint,
    @CreatedBy varchar(50)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM ChatKeywordAlias WHERE Alias = @Alias AND ServerID = @ServerID)
    BEGIN
        SELECT 'exists' AS Result;
        RETURN;
    END

    INSERT INTO ChatKeywordAlias (Alias, Keyword, ServerID, CreatedOn, CreatedBy)
    VALUES (@Alias, @Keyword, @ServerID, GETUTCDATE(), @CreatedBy);

    SELECT 'added' AS Result;
END
GO
