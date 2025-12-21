





-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[AddEvent]
	-- Add the parameters for the stored procedure here
	@EventName nvarchar(255),
	@EventDescription nvarchar(500),
	@EventDateTime datetime,
	@EventReminderTime int = 0,
	@EventChannelSource varchar(20),
	@CreatedBy varchar(2000)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	INSERT INTO Event([EventDateTime]
      ,[EventName]
      ,[EventDescription]
      ,[EventChannelSource]
      ,[CreatedOn]
      ,[CreatedBy])
	VALUES (@EventDateTime,  @EventName, @EventDescription, @EventChannelSource, GETDATE(), @CreatedBy)

END
