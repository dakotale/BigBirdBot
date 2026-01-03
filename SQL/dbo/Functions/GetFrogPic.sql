



-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[GetFrogPic](@RAND float) 
RETURNS varchar(500)
AS
BEGIN
	-- Declare the return variable here
	DECLARE @Result varchar(500);

	SET @Result = (SELECT TOP(1) 
		URL 
	FROM 
		Frog
	WHERE FrogID = @RAND)

	-- RAND() * (SELECT MAX(EricID) FROM Eric)

	-- Return the result of the function
	RETURN @Result

END
