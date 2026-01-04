
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetUsersScheduledKeyword]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	DECLARE @Now datetime = GETDATE();
    ------------------------------------------------------------------
    -- Calculate reusable date bounds
    ------------------------------------------------------------------
    DECLARE @FromDate datetime = DATEADD(HOUR, 1, CONVERT(datetime, CONVERT(date, @Now))); -- 1:00 AM
    DECLARE @ToDate datetime = DATEADD(HOUR, 23, CONVERT(datetime, CONVERT(date, @Now))); -- 11:00 PM
    DECLARE @Seconds int = DATEDIFF(SECOND, @FromDate, @ToDate);
	DECLARE @DueKeywords TABLE
	(
		UserID varchar(100),
		ChatKeyword nvarchar(255)
	);

    ------------------------------------------------------------------
    -- Identify due users
    ------------------------------------------------------------------
    INSERT INTO @DueKeywords (UserID, ChatKeyword)
	SELECT
		usk.UserID,
		usk.ChatKeyword
	FROM UsersScheduledKeyword usk
	WHERE usk.ScheduledDateTime <= GETDATE();
    ------------------------------------------------------------------
    -- Reschedule users
    ------------------------------------------------------------------
	DECLARE @RandomTime DATETIME = DATEADD(DAY, 1,DATEADD(SECOND,ABS(CHECKSUM(NEWID())) % @Seconds, @FromDate))

    UPDATE usk
    SET ScheduledDateTime = @RandomTime
    FROM UsersScheduledKeyword usk
    JOIN @DueKeywords d ON d.UserID = usk.UserID;
    ------------------------------------------------------------------
    -- Return randomized keyword results
    ------------------------------------------------------------------
    SELECT
        ck.FilePath,
        d.UserID,
        d.ChatKeyword AS [ThirstTable]
    FROM @DueKeywords d
    CROSS APPLY
    (
        SELECT TOP (1) FilePath
        FROM ChatKeyword
        WHERE ChatKeyword = d.ChatKeyword
        ORDER BY NEWID()
    ) ck

    ------------------------------------------------------------------
    -- Monday special
    ------------------------------------------------------------------
    UNION ALL
    SELECT
        'https://www.youtube.com/watch?v=QxCSQ0j-SFM',
        v.UserID,
        'DOTO MONDAY'
    FROM (VALUES
        ('233611778351824896'),
        ('171369791486033920')
    ) v(UserID)
    WHERE DATENAME(WEEKDAY, @Now) = 'Monday'
	AND EXISTS (SELECT 1 FROM @DueKeywords WHERE v.UserID = UserID)

    ------------------------------------------------------------------
    -- Friday special
    ------------------------------------------------------------------
    UNION ALL
    SELECT
        'https://www.youtube.com/watch?v=MGxMxko9hww',
        v.UserID,
        'MATIKANEFUKUKITARU FRIDAY'
    FROM (VALUES
        ('233611778351824896'),
        ('171369791486033920')
    ) v(UserID)
    WHERE DATENAME(WEEKDAY, @Now) = 'Friday'
	AND EXISTS (SELECT 1 FROM @DueKeywords WHERE v.UserID = UserID);
END
