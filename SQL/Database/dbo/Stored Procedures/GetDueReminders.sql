CREATE PROCEDURE [dbo].[GetDueReminders]
AS
BEGIN
    SET NOCOUNT ON;

    -- Atomically mark as sent and return them in one statement
    UPDATE [dbo].[Reminders]
    SET    [Sent] = 1
    OUTPUT INSERTED.[ReminderID],
           INSERTED.[UserID],
           INSERTED.[Message],
           INSERTED.[RemindAtUtc]
    WHERE  [Sent]        = 0
      AND  [RemindAtUtc] <= GETUTCDATE();
END
