CREATE TABLE [dbo].[BotAIMessage] (
    [BotAIMessageID] INT            IDENTITY (1, 1) NOT NULL,
    [UserID]         VARCHAR (50)   NOT NULL,
    [ServerUID]      VARCHAR (50)   NOT NULL,
    [ChatRole]       VARCHAR (10)   NOT NULL,
    [ChatMessage]    NVARCHAR (MAX) NOT NULL,
    [CreatedOn]      DATETIME       DEFAULT (getdate()) NOT NULL,
    [ChannelID]      VARCHAR (50)   NULL
);

