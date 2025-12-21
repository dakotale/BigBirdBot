




-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetPronounUsersByID]
	-- Add the parameters for the stored procedure here
	@UserID varchar(50),
	@PronounID int,
	@ServerID varchar(50)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT
		p.ID,
		p.Pronoun
	FROM
		PronounUsers pu
		JOIN Pronouns p ON pu.PronounID = p.ID
	WHERE
		pu.UserID = @UserID
		AND p.ID = @PronounID
		AND pu.ServerID = @ServerID

END
