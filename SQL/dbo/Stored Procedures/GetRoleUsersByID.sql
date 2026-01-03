





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetRoleUsersByID]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@RoleID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		p.ID,
		p.RoleID
	FROM
		RoleUsers pu
		JOIN Roles p ON pu.RoleID = p.RoleID
	WHERE
		pu.UserID = @UserID
		AND p.RoleID = @RoleID

END
