CREATE PROCEDURE [dbo].[GetPassiveJackpot]
    @ServerID BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ISNULL([Pool], 0) AS [Pool]
    FROM   [dbo].[PassiveJackpot]
    WHERE  [ServerID] = @ServerID;
END
