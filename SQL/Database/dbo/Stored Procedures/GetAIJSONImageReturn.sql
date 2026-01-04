
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetAIJSONImageReturn]
	-- Add the parameters for the stored procedure here
	@json nvarchar(max)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
		Status
		,CONVERT(float, Result) * 100.0	[PercentageChance]
	FROM OPENJSON(@json)
	WITH
	(
		Status varchar(50) '$.status'
		,Result varchar(50) '$.type.ai_generated'
	)
END
