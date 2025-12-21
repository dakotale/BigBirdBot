




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetRoleUsers]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		r.ID,
		r.RoleID,
		r.RoleName
	FROM
		RoleUsers ru
		JOIN Roles r ON ru.RoleID = r.ID
	WHERE
		ru.UserID = @UserID
		AND r.ServerUID = @ServerID

END
