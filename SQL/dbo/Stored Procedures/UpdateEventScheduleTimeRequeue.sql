-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateEventScheduleTimeRequeue]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @HasThirst bit = (SELECT TOP(1) 1 FROM EventScheduleTime WHERE UserID = @UserID)

	IF (@HasThirst = 0)
	BEGIN
		SELECT 'This user does not have any scheduled thirsts to be sent out.'	[Message]
	END

    -- Insert statements for procedure here
	IF ((SELECT COUNT(*) FROM EventScheduleTime WHERE UserID = @UserID) > 0)
	BEGIN
		UPDATE EventScheduleTime
		SET EventDateTime = DATEADD(minute, 1, GETDATE())
		WHERE UserID = @UserID

		SELECT 'The user was added successfully and the following thirst tables (' + (SELECT STRING_AGG(ScheduledEventTable, ', ') FROM UsersThirstTable WHERE UserID = @UserID) + ') will be sent at ' + CONVERT(varchar, DATEADD(minute, 1, GETDATE()), 108) [Message]
	END
	ELSE
	BEGIN
		INSERT INTO EventScheduleTime
		VALUES (DATEADD(minute, 1, GETDATE()), @UserID, 0)

		SELECT 'The user was added successfully and the following thirst tables (' + (SELECT STRING_AGG(ScheduledEventTable, ', ') FROM UsersThirstTable WHERE UserID = @UserID) + ') will be sent at ' + CONVERT(varchar, DATEADD(minute, 1, GETDATE()), 108) [Message]
	END
END
