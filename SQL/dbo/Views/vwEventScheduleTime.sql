



CREATE VIEW [dbo].[vwEventScheduleTime]
AS
select 
	u.UserID
	,u.Username
	,utt.ScheduledEventTable
	,est.EventDateTime
from
	EventScheduleTime est
	JOIN Users u ON est.UserID = u.UserID
	JOIN UsersThirstTable utt ON est.UserID = utt.UserID
