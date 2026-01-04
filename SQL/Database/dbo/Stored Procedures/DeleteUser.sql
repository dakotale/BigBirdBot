-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE DeleteUser
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@ServerID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @TotalCount INT = (SELECT COUNT(UserID) FROM Users WHERE UserID = @UserID)

    -- Insert statements for procedure here
	IF @TotalCount IS NOT NULL
	BEGIN
		DELETE FROM Users
		WHERE UserID = @UserID AND ServerUID = @ServerID

		IF @TotalCount = 1
		BEGIN
			DELETE FROM UsersScheduledKeyword
			WHERE UserID = @UserID

			DELETE FROM BotAIMessage
			WHERE UserID = @UserID
		END
	END
END
