
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetServerByID] 
	-- Add the parameters for the stored procedure here
	@ServerUID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT
		ServerUID,
		ServerName,
		DefaultChannelID,
		IsActive,
		Prefix
	FROM
		Servers
	WHERE
		ServerUID = @ServerUID

END
