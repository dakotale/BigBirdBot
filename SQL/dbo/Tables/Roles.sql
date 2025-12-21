CREATE TABLE [dbo].[Roles] (
    [ID]        INT           IDENTITY (1, 1) NOT NULL,
    [RoleName]  NVARCHAR (50) NOT NULL,
    [RoleID]    BIGINT        NOT NULL,
    [ServerUID] BIGINT        NOT NULL
);

