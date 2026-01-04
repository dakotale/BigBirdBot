CREATE TABLE [dbo].[Users] (
    [UserID]    VARCHAR (50)   NOT NULL,
    [Username]  NVARCHAR (100) NOT NULL,
    [JoinDate]  DATETIME       NOT NULL,
    [ServerUID] BIGINT         NOT NULL,
    [Nickname]  NVARCHAR (100) NULL,
    [PronounID] INT            NULL,
    [CreatedOn] DATETIME       NOT NULL,
    [DeletedOn] DATETIME       NULL
);


GO
CREATE CLUSTERED INDEX [IDX_USERS_USERID_SERVERUID_PRONOUNID]
    ON [dbo].[Users]([UserID] ASC, [ServerUID] ASC, [PronounID] ASC);

