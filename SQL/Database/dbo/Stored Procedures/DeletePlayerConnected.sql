


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeletePlayerConnected] 
	-- Add the parameters for the stored procedure here
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DELETE FROM PlayerConnected
	WHERE ServerUID = @ServerID

	UPDATE Servers
	SET IsPlayerConnected = 0
	WHERE ServerUID = @ServerID
END
