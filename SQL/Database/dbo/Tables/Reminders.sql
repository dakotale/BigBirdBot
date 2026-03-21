CREATE TABLE [dbo].[Reminders] (
    [ReminderID] INT           IDENTITY (1, 1) NOT NULL,
    [UserID]     VARCHAR (50)  NOT NULL,
    [Message]    NVARCHAR (500) NOT NULL,
    [RemindAtUtc] DATETIME     NOT NULL,
    [Sent]       BIT           NOT NULL DEFAULT (0),
    CONSTRAINT [PK_Reminders] PRIMARY KEY CLUSTERED ([ReminderID] ASC)
);
GO

CREATE INDEX [IX_Reminders_Due] ON [dbo].[Reminders] ([Sent], [RemindAtUtc]);
GO
