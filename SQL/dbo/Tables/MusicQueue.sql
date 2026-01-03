CREATE TABLE [dbo].[MusicQueue] (
    [MusicQueueID]   INT            IDENTITY (1, 1) NOT NULL,
    [MusicID]        INT            NOT NULL,
    [ServerUID]      BIGINT         NOT NULL,
    [VoiceChannelID] BIGINT         NOT NULL,
    [TextChannelID]  BIGINT         NOT NULL,
    [URL]            NVARCHAR (500) NOT NULL,
    [CreatedOn]      DATETIME       NOT NULL,
    [CreatedBy]      VARCHAR (50)   NOT NULL
);

