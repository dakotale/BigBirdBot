CREATE TABLE [dbo].[ChatKeywordMultiple] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [ChatKeyword] VARCHAR (50)   NOT NULL,
    [FilePath]    NVARCHAR (MAX) NOT NULL,
    [CreatedOn]   DATETIME       NOT NULL,
    [NSFW]        BIT            DEFAULT ((0)) NOT NULL
);

