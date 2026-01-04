CREATE TABLE [dbo].[ChatKeyword] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [ChatKeyword] VARCHAR (50)   NOT NULL,
    [FilePath]    NVARCHAR (MAX) NOT NULL,
    [CreatedOn]   DATETIME       NOT NULL,
    [NSFW]        BIT            DEFAULT ((0)) NOT NULL
);


GO
CREATE CLUSTERED INDEX [IDX_CHATKEYWORD_CHATKEYWORD_NSFW]
    ON [dbo].[ChatKeyword]([ChatKeyword] ASC, [NSFW] ASC);

