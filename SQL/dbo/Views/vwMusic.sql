



CREATE VIEW [dbo].[vwMusic]
AS
SELECT m.[MusicID]
      ,m.[ServerUID]
	  ,s.ServerName
      ,m.[VideoID]
      ,m.[Author]
      ,m.[Title]
      ,m.[URL]
      ,m.[CreatedOn]
      ,m.[CreatedBy]
  FROM [dbo].[Music] m
	JOIN [Servers] s ON m.ServerUID = s.ServerUID
WHERE
	m.CreatedBy != '171369791486033920'
