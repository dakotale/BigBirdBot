
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetScheduledEventUsers] 
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT DISTINCT
		Username
		,'Time' [ScheduledEventTable]
		,EventDateTime
	FROM
		vwEventScheduleTime
	ORDER BY
		EventDateTime
END
