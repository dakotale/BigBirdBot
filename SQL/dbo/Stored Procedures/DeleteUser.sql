


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteUser]
	-- Add the parameters for the stored procedure here
	@UserID nvarchar(50),
	@ServerID nvarchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	-- Check if User Exists in table
	--DECLARE @UserExists smallint = (SELECT TOP(1) 1 FROM Users WHERE UserID = @UserID)

    -- Insert statements for procedure here
	UPDATE Users
	SET DeletedOn = GETDATE()
	WHERE UserID = @UserID AND ServerUID = @ServerID
END
