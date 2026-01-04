-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE DeactiveServer
	-- Add the parameters for the stored procedure here
	@ServerID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE Servers
	SET IsActive = 0
	WHERE ServerUID = @ServerID

	IF @@ROWCOUNT > 0
	BEGIN
		-- Delete Keywords associated with the server
		DELETE FROM ChatKeywordMap 
		WHERE ServerID = @ServerID

		-- Remove Music Entries
		DELETE FROM Music
		WHERE ServerUID = @ServerID

		-- Delete Users
		DELETE FROM Users
		WHERE ServerUID = @ServerID
	END
END
