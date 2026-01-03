CREATE TABLE [dbo].[Users] (
    [UserID]    VARCHAR (50)   NOT NULL,
    [Username]  NVARCHAR (100) NOT NULL,
    [JoinDate]  DATETIME       NOT NULL,
    [ServerUID] BIGINT         NOT NULL,
    [Nickname]  NVARCHAR (100) NULL,
    [CreatedOn] DATETIME       NOT NULL,
    [DeletedOn] DATETIME       NULL
);

