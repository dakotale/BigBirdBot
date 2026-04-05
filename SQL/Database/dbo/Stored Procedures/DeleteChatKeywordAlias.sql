USE [DiscordBot]
GO

CREATE PROCEDURE [dbo].[DeleteChatKeywordAlias]
    @Alias    varchar(50),
    @ServerID bigint
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM ChatKeywordAlias
    WHERE Alias = @Alias AND ServerID = @ServerID;
END
GO
