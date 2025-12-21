





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetEvent]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	-- Only Testing in one server right now
	DECLARE @IsBirthday bit = (SELECT TOP(1) 1 FROM Birthday b WHERE FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm') = FORMAT(b.BirthdayDate, 'yyyy-MM-dd HH:mm'))

	IF @IsBirthday = 1
	BEGIN
		SELECT DISTINCT
			0	[EventID]
			,CASE
				WHEN b.BirthdayGuild = '877021430548664341'
				THEN '1028486843286700052'
				WHEN b.BirthdayGuild = '1057033598940745728'
				THEN '1118088254521090114'
				WHEN b.BirthdayGuild = '1250109007834906634'
				THEN '1250109008438890498'
				WHEN b.BirthdayGuild = '839226168661377094'
				THEN '839226168661377097'
				WHEN b.BirthdayGuild = '1317158440862351410'
				THEN '1317158442418307104'
				WHEN b.BirthdayGuild = '880569055856185354'
				THEN '1156625507840954369'
				ELSE ''
			END AS [EventChannelSource]
			,'**Happy Birthday** ' + b.BirthdayUser	[EventText]
		FROM
			Birthday b
		WHERE FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm') = FORMAT(b.BirthdayDate, 'yyyy-MM-dd HH:mm') 
	END
	ELSE
	BEGIN
		SELECT
			e.EventID
			,e.EventName
			,e.EventDescription
			,e.EventDateTime
			,E.EventChannelSource
			,'__**Event/Reminder Time!**__ ' + e.CreatedBy + CHAR(10) + '**' + TRIM(EventName) + '**'  + CHAR(10) + 'Description: ' + TRIM(EventDescription) [EventText]
			,e.CreatedOn
			,e.CreatedBy
		FROM
			Event e
		WHERE
			FORMAT(GETDATE(), 'yyyy-MM-dd HH:mm') = FORMAT(e.EventDateTime, 'yyyy-MM-dd HH:mm')
	END
	
END
