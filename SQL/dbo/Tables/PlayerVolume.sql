CREATE TABLE [dbo].[PlayerVolume] (
    [PlayerVolumeID] INT    IDENTITY (1, 1) NOT NULL,
    [ServerUID]      BIGINT NOT NULL,
    [Volume]         INT    NOT NULL
);

