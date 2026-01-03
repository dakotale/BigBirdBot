CREATE TABLE [dbo].[Birthday] (
    [BirthdayID]    INT            IDENTITY (1, 1) NOT NULL,
    [BirthdayDate]  DATETIME       NOT NULL,
    [BirthdayUser]  NVARCHAR (100) NOT NULL,
    [BirthdayGuild] NVARCHAR (100) NOT NULL
);

