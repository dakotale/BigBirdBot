CREATE TABLE [dbo].[UsersScheduledKeyword] (
    [UserID]            VARCHAR (50)  NOT NULL,
    [ChatKeyword]       VARCHAR (100) NOT NULL,
    [ScheduledDateTime] DATETIME      NOT NULL
);


GO
CREATE CLUSTERED INDEX [IDX_USERSSCHEDULEDKEYWORD_CHATKEYWORD_SCHEDULEDDATETIME]
    ON [dbo].[UsersScheduledKeyword]([ChatKeyword] ASC, [ScheduledDateTime] ASC);

