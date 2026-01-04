

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetPlayerConnected] 
	-- Add the parameters for the stored procedure here
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		pc.ServerUID
		,s.ServerName
		,pc.VoiceChannelID
		,pc.TextChannelID
	FROM
		PlayerConnected pc
		JOIN Servers s ON pc.ServerUID = s.ServerUID
	ORDER BY
		s.ServerName
END
