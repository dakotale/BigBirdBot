-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddServer] 
	-- Add the parameters for the stored procedure here
	@ServerUID bigint,
	@ServerName varchar(200),
	@DefaultChannelID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO [dbo].[Servers]
           ([ServerUID]
           ,[ServerName]
           ,[CreatedOn]
		   ,StayInVC
		   ,TwitterBroken
		   ,ShowWelcomeMessage
		   ,Prefix
		   ,DefaultChannelID
		   ,IsActive)
     VALUES
           (@ServerUID
           ,@ServerName
           ,GETDATE()
		   ,1
		   ,0
		   ,0
		   ,'-'
		   ,@DefaultChannelID
		   ,1)

	INSERT INTO dbo.PlayerVolume
	VALUES (@ServerUID, 50)
END
