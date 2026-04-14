from __future__ import annotations

from app.agents.base import AgentResult, NlpAgent
from app.models import DadosExtraidosModel, InferenceRequest, NlpProcessResponse


class InferenceService:
    def __init__(self, agents: list[NlpAgent]) -> None:
        self._agents = agents

    def process(self, request: InferenceRequest) -> NlpProcessResponse:
        merged = DadosExtraidosModel()
        best_confidence = 0.0
        chosen_intent: str | None = None
        debug_agents: list[dict[str, object]] = []

        for agent in self._agents:
            result = agent.analyze(request.Texto, request.TenantType)
            if not result.has_signal:
                continue

            self._merge_data(merged, result.dados)
            if result.confidence >= best_confidence:
                best_confidence = result.confidence
                chosen_intent = result.intent or chosen_intent

            debug_agents.append(
                {
                    "name": result.agent_name,
                    "confidence": round(result.confidence, 4),
                    "intent": result.intent,
                    "debug": result.debug,
                }
            )

        if chosen_intent is None:
            chosen_intent = self._derive_intent(merged)

        return NlpProcessResponse(
            Intencao=chosen_intent,
            DadosExtraidos=merged,
            Confianca=round(best_confidence, 4),
            Camada="NLP-Agent-Orchestrator",
            Debug={"agents": debug_agents},
        )

    @staticmethod
    def _merge_data(target: DadosExtraidosModel, source: DadosExtraidosModel) -> None:
        target_dict = target.model_dump()
        source_dict = source.model_dump()
        for key, source_value in source_dict.items():
            target_value = target_dict[key]
            if target_value in (None, False, "") and source_value not in (None, False, ""):
                setattr(target, key, source_value)

    @staticmethod
    def _derive_intent(dados: DadosExtraidosModel) -> str | None:
        if dados.Nome or dados.NomeVisitante or dados.Unidade or dados.Documento or dados.Cpf or dados.Empresa:
            return "Identificacao"

        return None