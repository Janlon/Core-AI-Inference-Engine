-- AIPort Adapter Orchestrator - Schema base para MariaDB
-- Charset/Collation recomendados para PT-BR.
CREATE DATABASE IF NOT EXISTS aiport
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE aiport;

CREATE TABLE IF NOT EXISTS Tenants (
  Id INT NOT NULL AUTO_INCREMENT,
  Pid INT NOT NULL,
  NomeIdentificador VARCHAR(120) NOT NULL,
  TipoLocal ENUM('RESIDENCIAL','COMERCIAL','HOSPITALAR','INDUSTRIAL') NOT NULL DEFAULT 'RESIDENCIAL',
  SystemType VARCHAR(50) NOT NULL DEFAULT 'condominio',
  WebhookUrl VARCHAR(500) NULL,
  ApiToken VARCHAR(255) NULL,
  SipTrunkPrefix VARCHAR(40) NULL,
  RamalTransfHumano VARCHAR(20) NULL,
  UsaBloco TINYINT(1) NOT NULL DEFAULT 0,
  UsaTorre TINYINT(1) NOT NULL DEFAULT 0,
  RecordingEnabled TINYINT(1) NOT NULL DEFAULT 0,
  AiProfile ENUM('AGRESSIVO','CONSERVADOR','ULTRA_ESTAVEL') NOT NULL DEFAULT 'CONSERVADOR',
  AiRegexConfidenceThreshold DECIMAL(5,4) NULL,
  AiNlpConfidenceThreshold DECIMAL(5,4) NULL,
  AiGlobalConfidenceThreshold DECIMAL(5,4) NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_Tenants_Pid (Pid)
) ENGINE=InnoDB;

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

CREATE TABLE IF NOT EXISTS OutboundWebhooks (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  TenantId INT NOT NULL,
  SessionId VARCHAR(100) NOT NULL,
  Url VARCHAR(500) NOT NULL,
  PayloadJson JSON NOT NULL,
  StatusCode INT NULL,
  Success TINYINT(1) NOT NULL DEFAULT 0,
  AttemptCount INT NOT NULL DEFAULT 1,
  ResponseBody TEXT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY IX_OutboundWebhooks_TenantId_CreatedAt (TenantId, CreatedAt),
  CONSTRAINT FK_OutboundWebhooks_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

INSERT INTO Tenants (Pid, NomeIdentificador, TipoLocal, SystemType, WebhookUrl, ApiToken, SipTrunkPrefix, RamalTransfHumano, UsaBloco, UsaTorre, RecordingEnabled, IsActive)
VALUES (200, 'Condominio Monte Verde', 'RESIDENCIAL', 'condominio', 'http://localhost:18080/webhook/aiport', 'dev-token', 'SIP/100', '9000', 1, 0, 0, 1)
ON DUPLICATE KEY UPDATE
  NomeIdentificador = VALUES(NomeIdentificador),
  TipoLocal = VALUES(TipoLocal),
  SystemType = VALUES(SystemType),
  WebhookUrl = VALUES(WebhookUrl),
  ApiToken = VALUES(ApiToken),
  SipTrunkPrefix = VALUES(SipTrunkPrefix),
  RamalTransfHumano = VALUES(RamalTransfHumano),
  UsaBloco = VALUES(UsaBloco),
  UsaTorre = VALUES(UsaTorre),
  RecordingEnabled = VALUES(RecordingEnabled),
  IsActive = VALUES(IsActive);
