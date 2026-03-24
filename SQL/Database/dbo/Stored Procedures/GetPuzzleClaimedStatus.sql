CREATE PROCEDURE [dbo].[GetPuzzleClaimedStatus]
    @ChannelID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    -- Returns the most recent puzzle for this channel regardless of expiry,
    -- so we can distinguish "solved" from "expired unsolved" at T+55.
    SELECT TOP 1 [Claimed]
    FROM  [dbo].[PetWordPuzzle]
    WHERE [ChannelID] = @ChannelID
    ORDER BY [PuzzleID] DESC;
END
