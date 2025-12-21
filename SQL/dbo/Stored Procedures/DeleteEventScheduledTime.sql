

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteEventScheduledTime]
(
	@UserID bigint,
	@TableName varchar(50)=''
)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	IF (LEN(@TableName) > 0)
	BEGIN
		DELETE FROM UsersThirstTable 
		WHERE UserID = @UserID AND ScheduledEventTable = @TableName

		SELECT 'This user will no longer receive scheduled thirst events for ' + @TableName + '.'	[Message]
	END
	ELSE
	BEGIN
		DELETE FROM UsersThirstTable 
		WHERE UserID = @UserID

		DELETE FROM EventScheduleTime
		WHERE UserID = @UserID

		SELECT 'This user was removed from scheduled thirst events.' [Message]
	END

END
