-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddAudit] 
	-- Add the parameters for the stored procedure here
	@Command varchar(50),
	@CreatedBy varchar(50),
	@ServerID bigint = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO [dbo].[AuditLog]
           ([Command]
           ,[CreatedOn]
           ,[CreatedBy]
		   ,ServerUID)
     VALUES
           (@Command
           ,GETDATE()
           ,@CreatedBy
		   ,@ServerID)
END
