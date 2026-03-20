CREATE TABLE [dbo].[PassiveJackpotContributors] (
    [ServerID] BIGINT       NOT NULL,
    [UserID]   VARCHAR (50) NOT NULL,
    CONSTRAINT [PK_PassiveJackpotContributors] PRIMARY KEY CLUSTERED ([ServerID] ASC, [UserID] ASC)
);
