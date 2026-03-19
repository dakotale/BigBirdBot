CREATE PROCEDURE [dbo].[ClaimPassiveJackpot]
    @ServerID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Claimed DECIMAL(20, 0) = 0;

    -- Atomically capture the current pool and reset it to zero.
    -- Returns the pre-reset amount so the caller can award the correct credits.
    UPDATE [dbo].[PassiveJackpot]
    SET    @Claimed = [Pool],
           [Pool]   = 0
    WHERE  [ServerID] = @ServerID;

    SELECT @Claimed AS [Pool];
END
