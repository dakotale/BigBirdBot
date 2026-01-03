





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddRoles]
	-- Add the parameters for the stored procedure here
	@RoleID bigint,
	@RoleName nvarchar(50),
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO Roles
	VALUES (@RoleName, @RoleID, @ServerID)

END
