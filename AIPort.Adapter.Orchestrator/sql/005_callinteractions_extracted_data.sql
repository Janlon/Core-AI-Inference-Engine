-- Adiciona coluna para persistir os dados extraidos por interacao.
ALTER TABLE CallInteractions
  ADD COLUMN IF NOT EXISTS ExtractedDataJson JSON NULL
  AFTER ResolutionLayer;
