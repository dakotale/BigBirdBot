USE [DiscordBot]
GO

CREATE PROCEDURE [dbo].[GetChatKeywordAliases]
    @Keyword  varchar(50),
    @ServerID bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Alias,
        Keyword,
        CreatedBy,
        CreatedOn
    FROM
        ChatKeywordAlias
    WHERE
        Keyword  = @Keyword
        AND ServerID = @ServerID
    ORDER BY
        Alias;
END
GO
