CREATE PROCEDURE [dbo].[AddReminder]
    @UserID      VARCHAR (50),
    @Message     NVARCHAR (500),
    @RemindAtUtc DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Reminders] ([UserID], [Message], [RemindAtUtc])
    VALUES (@UserID, @Message, @RemindAtUtc);
END
