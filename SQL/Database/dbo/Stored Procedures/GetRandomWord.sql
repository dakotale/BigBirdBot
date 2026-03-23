CREATE PROCEDURE [dbo].[GetRandomWord]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 [Word]
    FROM  [dbo].[Words]
    ORDER BY NEWID();
END
