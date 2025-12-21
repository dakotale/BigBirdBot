



-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[CheckIfEventExists]
	-- Add the parameters for the stored procedure here
	@EventDateTime datetime,
	@CreatedBy varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		EventID
		,EventName
		,EventDateTime
	FROM
		Event
	WHERE
		EventDateTime = @EventDateTime
		AND CreatedBy = @CreatedBy
END
