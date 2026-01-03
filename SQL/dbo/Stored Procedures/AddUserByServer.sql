
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddUserByServer]
	-- Add the parameters for the stored procedure here
	@UserID nvarchar(50),
	@Username nvarchar(100),
	@JoinDate datetime,
	@ServerUID bigint,
	@Nickname nvarchar(100) = null
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @UserExists smallint = (SELECT TOP(1) 1 FROM Users WHERE UserID = @UserID AND ServerUID = @ServerUID)

	-- Check if User Exists in table
	IF @UserExists IS NULL
	BEGIN
	INSERT INTO [dbo].[UsersByServer]
           ([UserID]
           ,[Username]
           ,[JoinDate]
		   ,ServerUID
           ,[Nickname]
           ,[CreatedOn]
		   ,DeletedOn)
     VALUES(
           @UserID
           ,@Username
           ,@JoinDate
		   ,@ServerUID
           ,@Nickname
           ,GETDATE()
		   ,NULL
		)
	END
END
