




CREATE VIEW [dbo].[vwAuditLog]
AS
	SELECT
		audit.ServerUID
		,audit.ServerName
		,audit.Command
		,audit.CreatedByUser
		,audit.CreatedByNickname
		,audit.IsKeyword
		,audit.CreatedOn
		,audit.CreatedBy
	FROM
	(
		SELECT 
			al.[ServerUID]
			,s.ServerName
			,al.[Command]
			,al.[CreatedOn]
			,al.[CreatedBy]
			,u.Username	[CreatedByUser]
			,u.Nickname	[CreatedByNickname]
			,0	[IsKeyword]
		FROM 
			[dbo].[AuditLog] al
			JOIN Servers s ON al.ServerUID = s.ServerUID
			JOIN Users u ON al.CreatedBy = u.UserID
	) audit
	WHERE
		audit.CreatedBy <> '171369791486033920'
