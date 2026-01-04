CREATE TABLE [dbo].[Servers] (
    [ServerID]          INT           IDENTITY (1, 1) NOT NULL,
    [ServerUID]         BIGINT        NOT NULL,
    [ServerName]        VARCHAR (200) NOT NULL,
    [DefaultChannelID]  BIGINT        NULL,
    [Volume]            INT           DEFAULT ((100)) NOT NULL,
    [FixEmbed]          BIT           NOT NULL,
    [IsPlayerConnected] BIT           DEFAULT ((0)) NOT NULL,
    [IsActive]          BIT           DEFAULT ((1)) NOT NULL,
    [CreatedOn]         DATETIME      NOT NULL
);


GO
CREATE CLUSTERED INDEX [IDX_SERVERS]
    ON [dbo].[Servers]([ServerUID] ASC, [DefaultChannelID] ASC, [Volume] ASC, [FixEmbed] ASC, [IsPlayerConnected] ASC, [IsActive] ASC);

