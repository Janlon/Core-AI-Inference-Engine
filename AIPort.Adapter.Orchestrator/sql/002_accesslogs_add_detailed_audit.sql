USE aiport;

CREATE TABLE IF NOT EXISTS CallSessions (
  SessionId VARCHAR(100) NOT NULL,
  TenantId INT NOT NULL,
  CallerId VARCHAR(60) NULL,
  Channel VARCHAR(120) NULL,
  StartedAt DATETIME NOT NULL,
  EndedAt DATETIME NULL,
  FinalAction VARCHAR(100) NULL,
  FinalExtractedData JSON NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (SessionId),
  KEY IX_CallSessions_TenantId_StartedAt (TenantId, StartedAt),
  CONSTRAINT FK_CallSessions_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS CallInteractions (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  SessionId VARCHAR(100) NOT NULL,
  InteractionOrder INT NOT NULL,
  BotPrompt TEXT NOT NULL,
  UserTranscription TEXT NULL,
  ResolutionLayer VARCHAR(30) NULL,
  ExtractedDataJson JSON NULL,
  InteractionDurationMs BIGINT NOT NULL,
  LlmProcessingTimeMs BIGINT NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY IX_CallInteractions_SessionId_Order (SessionId, InteractionOrder),
  CONSTRAINT FK_CallInteractions_CallSessions FOREIGN KEY (SessionId) REFERENCES CallSessions(SessionId)
    ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

DROP TABLE IF EXISTS AccessLogs;
