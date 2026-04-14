from __future__ import annotations

from typing import Any

from pydantic import BaseModel, Field


class DadosExtraidosModel(BaseModel):
    Nome: str | None = None
    NomeVisitante: str | None = None
    Documento: str | None = None
    Cpf: str | None = None
    Unidade: str | None = None
    Bloco: str | None = None
    Torre: str | None = None
    Empresa: str | None = None
    Parentesco: str | None = None
    EstaComVeiculo: bool = False
    Placa: str | None = None
    EEntregador: bool = False


class InferenceRequest(BaseModel):
    Texto: str = Field(min_length=1, max_length=2000)
    TenantType: str = "residential"
    SessionId: str | None = None
    Metadata: dict[str, str] | None = None


class NlpProcessResponse(BaseModel):
    Intencao: str | None = None
    DadosExtraidos: DadosExtraidosModel = Field(default_factory=DadosExtraidosModel)
    Confianca: float = Field(default=0.0, ge=0.0, le=1.0)
    Camada: str = "NLP-Agent-Orchestrator"
    Debug: dict[str, Any] | None = None


class HealthResponse(BaseModel):
    status: str
    service: str
    utc: str