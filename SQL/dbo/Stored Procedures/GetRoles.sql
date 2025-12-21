




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetRoles]
	-- Add the parameters for the stored procedure here
	(
		@ServerID bigint
	)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		ID,
		RoleName,
		RoleID
	FROM
		Roles
	WHERE
		ServerUID = @ServerID
		AND RoleName != 'thirsting'
	ORDER BY
		RoleName

END
