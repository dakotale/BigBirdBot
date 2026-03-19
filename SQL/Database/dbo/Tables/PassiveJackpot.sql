CREATE TABLE [dbo].[PassiveJackpot] (
    [ServerID] BIGINT         NOT NULL,
    [Pool]     DECIMAL(20, 0) NOT NULL CONSTRAINT [DF_PassiveJackpot_Pool] DEFAULT (0),
    CONSTRAINT [PK_PassiveJackpot] PRIMARY KEY CLUSTERED ([ServerID] ASC)
);
