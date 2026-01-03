CREATE TABLE [dbo].[ThirstMap] (
    [ID]            INT          IDENTITY (1, 1) NOT NULL,
    [AddKeyword]    VARCHAR (50) NOT NULL,
    [ChatKeywordID] INT          NOT NULL,
    [CreatedOn]     DATETIME     NOT NULL,
    [CreatedBy]     VARCHAR (50) NULL
);

