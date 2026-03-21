CREATE PROCEDURE [dbo].[GetPetWordPuzzle]
    @ChannelID VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        [PuzzleID],
        [Word],
        [ExpiresAt]
    FROM  [dbo].[PetWordPuzzle]
    WHERE [ChannelID] = @ChannelID
      AND [Claimed]   = 0
      AND [ExpiresAt] > GETUTCDATE();
END
