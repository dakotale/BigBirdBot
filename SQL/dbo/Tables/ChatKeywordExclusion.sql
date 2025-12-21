CREATE TABLE [dbo].[ChatKeywordExclusion] (
    [ID]        INT          IDENTITY (1, 1) NOT NULL,
    [UserID]    VARCHAR (50) NOT NULL,
    [ServerUID] BIGINT       NOT NULL,
    [CreatedOn] DATETIME     NOT NULL
);

