CREATE TABLE [dbo].[Event] (
    [EventID]            INT            IDENTITY (1, 1) NOT NULL,
    [EventDateTime]      DATETIME       NOT NULL,
    [EventName]          VARCHAR (255)  NOT NULL,
    [EventDescription]   VARCHAR (500)  NOT NULL,
    [EventChannelSource] VARCHAR (20)   NOT NULL,
    [CreatedOn]          DATETIME       NOT NULL,
    [CreatedBy]          VARCHAR (2000) NOT NULL
);

