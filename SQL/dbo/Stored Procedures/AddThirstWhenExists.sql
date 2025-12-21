-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddThirstWhenExists]
	-- Add the parameters for the stored procedure here
	@ServerID bigint,
	@Keyword varchar(50),
	@AddKeyword varchar(50),
	@CreatedBy varchar(50),
	@TableName varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO ChatKeyword
	VALUES (@ServerID, @Keyword, GETDATE(), 1, 0, @CreatedBy)

	DECLARE @ChatKeywordID int = (SELECT MAX(ChatKeywordID) FROM ChatKeyword)

	INSERT INTO ChatAction
	VALUES (@ChatKeywordID, '', GETDATE(), @CreatedBy)

	INSERT INTO [dbo].[ThirstMap]
           ([AddKeyword]
           ,[ChatKeywordID]
           ,[CreatedOn]
           ,[CreatedBy])
     VALUES
           (@AddKeyword
           ,@ChatKeywordID
           ,GETDATE()
           ,@CreatedBy)
END
