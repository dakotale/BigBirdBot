



-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[TurnAllOnOffKeywordsByServer]
	-- Add the parameters for the stored procedure here
	@ServerID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	UPDATE ChatKeyword
	SET IsMassDisabled = ~IsMassDisabled
	WHERE ServerID = @ServerID

	UPDATE Servers
	SET IsActive = ~IsActive
	WHERE ServerID = @ServerID

	SELECT
		CASE 
			WHEN (SELECT TOP(1) IsMassDisabled FROM ChatKeyword WHERE ServerID = @ServerID) = 1
			THEN 'All Keywords for the server are **disabled**.'
			ELSE 'All keywords for the server are **enabled**.'
		END AS [Result]

END
