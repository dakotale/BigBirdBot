CREATE PROCEDURE [dbo].[FeedPassiveJackpot]
    @ServerID BIGINT,
    @Amount   DECIMAL(20, 0)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[PassiveJackpot] WHERE [ServerID] = @ServerID)
        UPDATE [dbo].[PassiveJackpot]
        SET    [Pool] = [Pool] + @Amount
        WHERE  [ServerID] = @ServerID;
    ELSE
        INSERT INTO [dbo].[PassiveJackpot] ([ServerID], [Pool])
        VALUES (@ServerID, @Amount);
END
