
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateUsersScheduledKeywordRequeue]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @HasThirst bit = (SELECT TOP(1) 1 FROM UsersScheduledKeyword WHERE UserID = @UserID)

	IF (@HasThirst = 0)
	BEGIN
		SELECT 'This user does not have any scheduled thirsts to be sent out.'	[Message]
	END

    -- Insert statements for procedure here
	IF ((SELECT COUNT(*) FROM UsersScheduledKeyword WHERE UserID = @UserID) > 0)
	BEGIN
		UPDATE UsersScheduledKeyword
		SET ScheduledDateTime = DATEADD(minute, 1, GETDATE())
		WHERE UserID = @UserID

		SELECT 'The user was added successfully and the following keywords (' + (SELECT STRING_AGG(ChatKeyword, ', ') FROM UsersScheduledKeyword WHERE UserID = @UserID) + ') will be sent at ' + CONVERT(varchar, DATEADD(minute, 1, GETDATE()), 108) [Message]
	END
	ELSE
	BEGIN
		INSERT INTO UsersScheduledKeyword
		VALUES (DATEADD(minute, 1, GETDATE()), @UserID, 0)

		SELECT 'The user was added successfully and the following keywords (' + (SELECT STRING_AGG(ChatKeyword, ', ') FROM UsersScheduledKeyword WHERE UserID = @UserID) + ') will be sent at ' + CONVERT(varchar, DATEADD(minute, 1, GETDATE()), 108) [Message]
	END
END
