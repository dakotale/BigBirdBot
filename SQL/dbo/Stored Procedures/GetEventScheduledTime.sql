-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetEventScheduledTime]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @CurrentTime datetime = GETDATE()

	SELECT
		u.UserID
		,utt.ScheduledEventTable
	INTO
		#tempSilly
	FROM
		EventScheduleTime est
		JOIN Users u ON est.UserID = u.UserID
		JOIN UsersThirstTable utt ON u.UserID = utt.UserID
	WHERE
		FORMAT(est.EventDateTime, 'yyyy-MM-dd HH:mm') <= FORMAT(@CurrentTime, 'yyyy-MM-dd HH:mm')

	IF (SELECT COUNT(*) FROM #tempSilly) > 0
	BEGIN
		DECLARE @TableName nvarchar(100)
		DECLARE @SQL nvarchar(max)
		DECLARE @UserID varchar(100)
		DECLARE c CURSOR
		FOR SELECT UserID,ScheduledEventTable FROM #tempSilly;

		OPEN c
		FETCH NEXT FROM c INTO
			@UserID, @TableName;

		WHILE @@FETCH_STATUS = 0
		BEGIN
			-- 1:00 AM
			DECLARE @FromDate datetime = dateadd(hour, 1, convert(datetime, convert(date, getdate()))) 
			-- 11:00 PM
			DECLARE @ToDate datetime = dateadd(hour, 23, convert(datetime, convert(date, getdate()))) 

			DECLARE @Seconds int = DATEDIFF(SECOND, @FromDate, @ToDate)

			DECLARE @Random datetime = DATEADD(day, 1, DATEADD(SECOND, ROUND(((@Seconds-1) * RAND()), 0), @FromDate))

			-- Give me a randomized time and insert into the EventScheduleTime table with the user ID
			-- For now hardcoded, TODO: Use Users table to pull where ScheduledTable is not null
			UPDATE EventScheduleTime
			SET EventDateTime = @Random
			WHERE UserID = @UserID

			IF (LEN(@SQL) > 0)
			BEGIN
				SET @SQL += ' UNION SELECT FilePath, ' + @UserID + ' AS UserID, ''' + @TableName + ''' AS [ThirstTable] FROM ChatKeywordMultiple WHERE ID = (SELECT TOP(1) ID FROM ChatKeywordMultiple WHERE ChatKeyword = ''' + @TableName + ''' ORDER BY NEWID())'
			END
			ELSE
			BEGIN
				SET @SQL = 'SELECT FilePath, ' + @UserID + ' AS UserID, ''' + @TableName + ''' AS [ThirstTable] FROM ChatKeywordMultiple WHERE ID = (SELECT TOP(1) ID FROM ChatKeywordMultiple WHERE ChatKeyword = ''' + @TableName + ''' ORDER BY NEWID())'
			END

			FETCH NEXT FROM c INTO
				@UserID, @TableName;
		END;

		CLOSE c;
		DEALLOCATE c;

		IF (@UserID = '233611778351824896' OR @UserID = '171369791486033920') AND DATENAME(WEEKDAY, GETDATE()) = 'Monday'
		BEGIN
			SET @SQL += ' UNION SELECT ''https://www.youtube.com/watch?v=QxCSQ0j-SFM''	[FilePath], ' + @UserID + ' AS UserID, ''DOTO MONDAY'' AS [ThirstTable]'
		END

		IF (@UserID = '233611778351824896' OR @UserID = '171369791486033920') AND DATENAME(WEEKDAY, GETDATE()) = 'Friday'
		BEGIN
			SET @SQL += ' UNION SELECT ''https://www.youtube.com/watch?v=MGxMxko9hww''	[FilePath], ' + @UserID + ' AS UserID, ''MATIKANEFUKUKITARU FRIDAY'' AS [ThirstTable]'
		END

		exec sp_executesql @SQL

		DROP TABLE #tempSilly
	END
END
