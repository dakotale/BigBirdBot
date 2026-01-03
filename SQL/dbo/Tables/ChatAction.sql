CREATE TABLE [dbo].[ChatAction] (
    [ChatActionID]  INT             IDENTITY (1, 1) NOT NULL,
    [ChatKeywordID] INT             NOT NULL,
    [ChatAction]    NVARCHAR (2000) NULL,
    [CreatedOn]     DATETIME        NOT NULL,
    [CreatedBy]     VARCHAR (50)    NULL
);


GO
CREATE NONCLUSTERED INDEX [IDX_ChatKeywordID]
    ON [dbo].[ChatAction]([ChatKeywordID] ASC);

