CREATE PROCEDURE [dbo].[DrawPassiveJackpot]
    @ServerID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- Bail out if the pool is empty.
    DECLARE @Pool DECIMAL(20, 0) = 0;
    SELECT @Pool = ISNULL([Pool], 0)
    FROM   [dbo].[PassiveJackpot]
    WHERE  [ServerID] = @ServerID;

    IF @Pool <= 0 RETURN;

    -- Pick a random eligible contributor.
    DECLARE @WinnerID VARCHAR(50);
    SELECT TOP 1 @WinnerID = [UserID]
    FROM   [dbo].[PassiveJackpotContributors]
    WHERE  [ServerID] = @ServerID
    ORDER BY NEWID();

    IF @WinnerID IS NULL RETURN;

    -- Reset the pool and clear contributors atomically.
    UPDATE [dbo].[PassiveJackpot]
    SET    [Pool] = 0
    WHERE  [ServerID] = @ServerID;

    DELETE FROM [dbo].[PassiveJackpotContributors]
    WHERE  [ServerID] = @ServerID;

    SELECT @WinnerID AS [UserID], @Pool AS [Pool];
END
