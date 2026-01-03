
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddEventScheduledTime]
(
	@UserID bigint,
	@TableName varchar(50)
)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @ThirstExist smallint = (SELECT TOP(1) 1 FROM UsersThirstTable WHERE UserID = @UserID AND ScheduledEventTable = @TableName)

	IF @ThirstExist IS NULL
	BEGIN
		INSERT INTO UsersThirstTable (UserID, ScheduledEventTable)
		VALUES (@UserID, @TableName)
	END

	DECLARE @UserExists smallint =  (SELECT TOP(1) 1 FROM EventScheduleTime WHERE UserID = @UserID AND Activated = 0)

	IF @UserExists IS NULL
	BEGIN
		DECLARE @CurrentTime datetime = GETDATE()
		
		DECLARE @Random datetime = DATEADD(minute, 1, @CurrentTime)

		IF (SELECT COUNT(*) FROM EventScheduleTime WHERE FORMAT(@Random, 'yyyy-MM-dd HH:mm') = FORMAT(EventDateTime, 'yyyy-MM-dd HH:mm')) > 0
		BEGIN
			SET @Random = DATEADD(minute, 2, @CurrentTime)
		END

		INSERT INTO EventScheduleTime (EventDateTime, UserID, Activated)
		VALUES( @Random, @UserID, 0)

		SELECT EventDateTime	[ScheduleTime], STRING_AGG(utt.ScheduledEventTable, ', ')	[ScheduledEventTable] FROM EventScheduleTime est JOIN UsersThirstTable utt ON est.UserID = utt.UserID WHERE est.UserID = @UserID AND Activated = 0 GROUP BY est.UserID, est.EventDateTime
	END
	ELSE
	BEGIN
		SELECT EventDateTime	[ScheduleTime], STRING_AGG(utt.ScheduledEventTable, ', ')	[ScheduledEventTable] FROM EventScheduleTime est JOIN UsersThirstTable utt ON est.UserID = utt.UserID WHERE est.UserID = @UserID AND Activated = 0 GROUP BY est.UserID, est.EventDateTime
	END
END
