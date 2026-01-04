CREATE TABLE [dbo].[Music] (
    [MusicID]   INT            IDENTITY (1, 1) NOT NULL,
    [ServerUID] BIGINT         NOT NULL,
    [VideoID]   NVARCHAR (100) NOT NULL,
    [Author]    NVARCHAR (100) NOT NULL,
    [Title]     NVARCHAR (200) NOT NULL,
    [URL]       NVARCHAR (500) NOT NULL,
    [CreatedOn] DATETIME       NOT NULL,
    [CreatedBy] VARCHAR (50)   NOT NULL
);


GO
CREATE NONCLUSTERED INDEX [IDX_MUSIC_SERVERUID]
    ON [dbo].[Music]([ServerUID] ASC);

