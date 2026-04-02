-- Adiciona coluna para rastrear camada de resolução da extração por interação.
ALTER TABLE CallInteractions
  ADD COLUMN IF NOT EXISTS ResolutionLayer VARCHAR(30) NULL
  AFTER UserTranscription;
