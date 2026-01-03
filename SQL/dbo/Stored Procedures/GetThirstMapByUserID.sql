
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[GetThirstMapByUserID]
	-- Add the parameters for the stored procedure here
	@UserID bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	DECLARE @IsThirst bit = (SELECT TOP(1) 1 FROM UsersThirstTable WHERE UserID = @UserID)

	IF @IsThirst = 1
	BEGIN
		SELECT
			utt.ScheduledEventTable	[TableList]
		FROM
			UsersThirstTable utt
		WHERE
			utt.UserID = @UserID
		ORDER BY
			TableList
	END
	ELSE
	BEGIN
		SELECT 'There are no scheduled thirst events for this user.'	[TableName]
	END
END
