

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[GetDogFromAPI]() 
RETURNS varchar(max)
AS
BEGIN
	-- Declare the return variable here
	DECLARE @URL NVARCHAR(MAX) = 'https://dog.ceo/api/breeds/image/random';
	Declare @Object as Int;
	Declare @ResponseText as Varchar(8000);
	DECLARE @Result varchar(500);

	Exec sp_OACreate 'MSXML2.XMLHTTP', @Object OUT;
	Exec sp_OAMethod @Object, 'open', NULL, 'get',
		   @URL,
		   'False'
	Exec sp_OAMethod @Object, 'send'
	Exec sp_OAMethod @Object, 'responseText', @ResponseText OUTPUT
	IF((Select @ResponseText) <> '')
	BEGIN
		 DECLARE @json NVARCHAR(MAX) = (Select @ResponseText)
		 SET @Result = (SELECT 
			DogPic
		FROM
			OPENJSON(@json)
			WITH
			(
				DogPic varchar(max) '$.message'
			))
	END
	ELSE
	BEGIN
		 DECLARE @ErroMsg NVARCHAR(30) = 'No data found.';
		 RETURN @ErroMsg;
	END
	Exec sp_OADestroy @Object

	-- Return the result of the function
	RETURN @Result

END
