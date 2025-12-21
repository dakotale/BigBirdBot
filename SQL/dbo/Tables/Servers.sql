CREATE TABLE [dbo].[Servers] (
    [ServerID]           INT           IDENTITY (1, 1) NOT NULL,
    [ServerUID]          BIGINT        NOT NULL,
    [ServerName]         VARCHAR (200) NOT NULL,
    [CreatedOn]          DATETIME      NOT NULL,
    [StayInVC]           BIT           NOT NULL,
    [TwitterBroken]      BIT           NOT NULL,
    [ShowWelcomeMessage] BIT           NOT NULL,
    [Prefix]             VARCHAR (10)  CONSTRAINT [DefaultPrefix] DEFAULT ('-') NOT NULL,
    [DefaultChannelID]   BIGINT        NULL,
    [IsActive]           BIT           DEFAULT ((1)) NOT NULL
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_ServerID]
    ON [dbo].[Servers]([ServerID] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IDX_ServerUID]
    ON [dbo].[Servers]([ServerUID] ASC);

