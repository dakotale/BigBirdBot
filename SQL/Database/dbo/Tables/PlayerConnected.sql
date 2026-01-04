CREATE TABLE [dbo].[PlayerConnected] (
    [PlayerID]       INT          IDENTITY (1, 1) NOT NULL,
    [ServerUID]      BIGINT       NOT NULL,
    [VoiceChannelID] BIGINT       NOT NULL,
    [TextChannelID]  BIGINT       NOT NULL,
    [CreatedOn]      DATETIME     NOT NULL,
    [CreatedBy]      VARCHAR (50) NOT NULL
);

