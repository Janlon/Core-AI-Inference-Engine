USE aiport;

ALTER TABLE CallSessions
  ADD COLUMN IF NOT EXISTS FinalReasonCode VARCHAR(120) NULL AFTER FinalAction,
  ADD COLUMN IF NOT EXISTS FinalReasonCategory VARCHAR(60) NULL AFTER FinalReasonCode,
  ADD COLUMN IF NOT EXISTS FinalReasonMessage TEXT NULL AFTER FinalReasonCategory,
  ADD COLUMN IF NOT EXISTS WebhookHttpStatus INT NULL AFTER FinalReasonMessage,
  ADD COLUMN IF NOT EXISTS WebhookPayloadHash CHAR(64) NULL AFTER WebhookHttpStatus,
  ADD COLUMN IF NOT EXISTS WebhookPayloadSentAt DATETIME NULL AFTER WebhookPayloadHash,
  ADD COLUMN IF NOT EXISTS WebhookCorrelationId VARCHAR(255) NULL AFTER WebhookPayloadSentAt,
  ADD COLUMN IF NOT EXISTS WebhookCorrelationField VARCHAR(80) NULL AFTER WebhookCorrelationId;

CREATE INDEX IF NOT EXISTS IX_CallSessions_FinalReasonCode_StartedAt ON CallSessions (FinalReasonCode, StartedAt);