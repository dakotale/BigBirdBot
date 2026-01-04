CREATE TABLE [dbo].[ChatKeywordMap] (
    [ID]         INT          IDENTITY (1, 1) NOT NULL,
    [AddKeyword] VARCHAR (50) NOT NULL,
    [ServerID]   BIGINT       NOT NULL,
    [CreatedOn]  DATETIME     NOT NULL,
    [CreatedBy]  VARCHAR (50) NULL,
    [Keyword]    AS           (replace([AddKeyword],'add',''))
);


GO
CREATE CLUSTERED INDEX [IDX_CHATKEYWORDMAP_ADDKEYWORD_KEYWORD_SERVERID]
    ON [dbo].[ChatKeywordMap]([AddKeyword] ASC, [ServerID] ASC, [Keyword] ASC);

