-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE AddLandmine
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@Username varchar(100)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO [dbo].[Landmine]
			   ([UserID]
			   ,[Username]
			   ,[CreatedOn])
	VALUES
		(@UserID
		,@Username
		,GETDATE())
END
