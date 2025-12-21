CREATE TABLE [dbo].[RoleUsers] (
    [UserID]    NVARCHAR (50) NOT NULL,
    [RoleID]    BIGINT        NOT NULL,
    [ServerUID] BIGINT        NOT NULL,
    [CreatedOn] DATETIME      NOT NULL
);

