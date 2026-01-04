
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddBirthday]
	-- Add the parameters for the stored procedure here
	@BirthdayDate datetime,
	@BirthdayUser nvarchar(100),
	@BirthdayGuild nvarchar(100)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	INSERT INTO Birthday
	VALUES (@BirthdayDate, @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 1, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 2, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 3, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 4, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 5, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 6, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 7, @BirthdayDate), @BirthdayUser, @BirthdayGuild)

	INSERT INTO Birthday
	VALUES (DATEADD(year, 8, @BirthdayDate), @BirthdayUser, @BirthdayGuild)
END
