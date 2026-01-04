
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateVolume]
	-- Add the parameters for the stored procedure here
	@ServerUID bigint,
	@Volume int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE Servers
	SET Volume = @Volume
	WHERE ServerUID = @ServerUID
END
