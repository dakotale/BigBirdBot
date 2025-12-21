
CREATE VIEW [dbo].[vwChatKeywordAction]
AS
SELECT ck.[ChatKeywordID]
      ,ck.[ServerID]
	  ,s.ServerName
      ,ck.[Keyword]
      ,ca.ChatAction
	  ,CASE WHEN tm.ID IS NOT NULL THEN 1 ELSE 0 END [IsThirstCommand]
	  ,ck.[IsActive]
      ,ck.[IsMassDisabled]
	  ,ck.[CreatedOn]
      ,ck.CreatedBy
  FROM [dbo].[ChatKeyword] ck
	JOIN ChatAction ca ON ck.ChatKeywordID = ca.ChatKeywordID
	JOIN Servers s ON ck.ServerID = s.ServerUID
	LEFT JOIN ThirstMap tm ON ck.ChatKeywordID = tm.ChatKeywordID
