
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
			  ,[DefaultChannelID]
			  ,[Volume]
			  ,[FixEmbed]
			  ,[IsPlayerConnected]
			  ,[IsActive]
			  ,[CreatedOn])
     VALUES
           (@ServerUID
           ,@ServerName
           ,@DefaultChannelID
		   ,100
		   ,0
		   ,0
		   ,1
		   ,GETDATE())
END
