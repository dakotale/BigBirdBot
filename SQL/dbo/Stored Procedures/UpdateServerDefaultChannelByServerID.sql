


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[UpdateServerDefaultChannelByServerID]
	-- Add the parameters for the stored procedure here
	@ServerUID bigint,
	@DefaultChannelID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    UPDATE Servers
	SET DefaultChannelID = @DefaultChannelID
	WHERE ServerUID = @ServerUID
END
