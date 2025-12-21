CREATE TABLE [dbo].[AuditLog] (
    [AuditLogID] INT          IDENTITY (1, 1) NOT NULL,
    [Command]    VARCHAR (50) NOT NULL,
    [ServerUID]  BIGINT       NOT NULL,
    [CreatedOn]  DATETIME     NOT NULL,
    [CreatedBy]  VARCHAR (50) NOT NULL
);

