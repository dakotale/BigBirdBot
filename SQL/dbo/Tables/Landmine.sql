CREATE TABLE [dbo].[Landmine] (
    [LandmineID] INT           IDENTITY (1, 1) NOT NULL,
    [UserID]     VARCHAR (50)  NOT NULL,
    [Username]   VARCHAR (100) NOT NULL,
    [CreatedOn]  DATETIME      DEFAULT (getdate()) NOT NULL
);

