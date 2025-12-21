





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddUsersThirstTableLog]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@FilePath nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO UsersThirstTableLog(UserID, FilePath, CreatedOn)
	VALUES (@UserID, @FilePath, GETDATE())

END
