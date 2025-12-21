




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteRoleUsers]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@RoleID bigint,
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DELETE FROM RoleUsers
	WHERE UserID = @UserID AND RoleID = @RoleID AND ServerUID = @ServerID

END
