CREATE TABLE [dbo].[ChatKeyword] (
    [ChatKeywordID]  INT          IDENTITY (1, 1) NOT NULL,
    [ServerID]       BIGINT       NOT NULL,
    [Keyword]        VARCHAR (50) NOT NULL,
    [CreatedOn]      DATETIME     NOT NULL,
    [IsActive]       BIT          NOT NULL,
    [IsMassDisabled] BIT          NOT NULL,
    [CreatedBy]      VARCHAR (50) NULL
);


GO
CREATE NONCLUSTERED INDEX [IDX_ServerID]
    ON [dbo].[ChatKeyword]([ServerID] ASC);

