
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddUsersScheduledKeyword]
(
	@UserID bigint,
	@Keyword varchar(50)
)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	IF EXISTS (SELECT 1 FROM UsersScheduledKeyword WHERE UserID = @UserID)
	BEGIN
		DECLARE @CurrentTime datetime = GETDATE()
		DECLARE @Random datetime = DATEADD(minute, 1, @CurrentTime)

		IF (SELECT COUNT(*) FROM UsersScheduledKeyword WHERE FORMAT(@Random, 'yyyy-MM-dd HH:mm') = FORMAT(ScheduledDateTime, 'yyyy-MM-dd HH:mm')) > 0
		BEGIN
			SET @Random = DATEADD(minute, 2, @CurrentTime)
		END

		INSERT INTO UsersScheduledKeyword (UserID, ChatKeyword, ScheduledDateTime)
		VALUES(@UserID, @Keyword, @Random)
	END


	SELECT
		usk.ScheduledDateTime	[ScheduleTime]
		,STRING_AGG(usk.ChatKeyword, ', ')	[ScheduledEventTable]
	FROM
		UsersScheduledKeyword  usk
	WHERE
		usk.UserID = @UserID
	GROUP BY
		usk.ChatKeyword, usk.ScheduledDateTime
END
