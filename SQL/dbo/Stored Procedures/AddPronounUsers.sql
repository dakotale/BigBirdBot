



-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddPronounUsers]
	-- Add the parameters for the stored procedure here
	@ServerID varchar(50),
	@UserID varchar(50),
	@PronounID int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO PronounUsers
	VALUES (@UserID, @PronounID, GETDATE(), @ServerID)

END
