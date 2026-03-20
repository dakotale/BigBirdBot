CREATE PROCEDURE [dbo].[FeedPassiveJackpot]
    @ServerID BIGINT,
    @UserID   VARCHAR (50),
    @Amount   DECIMAL(20, 0)
AS
BEGIN
    SET NOCOUNT ON;

    -- Update or create the pool row.
    IF EXISTS (SELECT 1 FROM [dbo].[PassiveJackpot] WHERE [ServerID] = @ServerID)
        UPDATE [dbo].[PassiveJackpot]
        SET    [Pool] = [Pool] + @Amount
        WHERE  [ServerID] = @ServerID;
    ELSE
        INSERT INTO [dbo].[PassiveJackpot] ([ServerID], [Pool])
        VALUES (@ServerID, @Amount);

    -- Register the user as an eligible winner for the next draw.
    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[PassiveJackpotContributors]
        WHERE [ServerID] = @ServerID AND [UserID] = @UserID)
    BEGIN
        INSERT INTO [dbo].[PassiveJackpotContributors] ([ServerID], [UserID])
        VALUES (@ServerID, @UserID);
    END
END
