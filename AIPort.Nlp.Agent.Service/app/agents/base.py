from __future__ import annotations

from dataclasses import dataclass, field
from typing import Protocol

from app.models import DadosExtraidosModel


@dataclass(slots=True)
class AgentResult:
    agent_name: str
    confidence: float = 0.0
    intent: str | None = None
    dados: DadosExtraidosModel = field(default_factory=DadosExtraidosModel)
    debug: dict[str, object] = field(default_factory=dict)

    @property
    def has_signal(self) -> bool:
        return self.confidence > 0 or any(value not in (None, False, "") for value in self.dados.model_dump().values())


class NlpAgent(Protocol):
    name: str

    def analyze(self, text: str, tenant_type: str) -> AgentResult:
        ...