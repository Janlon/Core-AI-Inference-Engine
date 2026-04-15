from __future__ import annotations

import re

from app.agents.base import AgentResult, NlpAgent
from app.models import DadosExtraidosModel, InferenceRequest, NlpProcessResponse


class InferenceService:
    def __init__(self, agents: list[NlpAgent]) -> None:
        self._agents = agents

    def process(self, request: InferenceRequest) -> NlpProcessResponse:
        merged = request.CurrentState.model_copy(deep=True) if request.CurrentState is not None else DadosExtraidosModel()
        best_confidence = 0.0
        chosen_intent: str | None = None
        debug_agents: list[dict[str, object]] = []
        working_text = request.OriginalText or request.Texto

        slot_result = self._extract_by_expected_slot(request, working_text)
        if slot_result is not None:
            self._merge_data(merged, slot_result.dados)
            if slot_result.confidence >= best_confidence:
                best_confidence = slot_result.confidence
                chosen_intent = slot_result.intent or chosen_intent
            debug_agents.append(
                {
                    "name": slot_result.agent_name,
                    "confidence": round(slot_result.confidence, 4),
                    "intent": slot_result.intent,
                    "debug": slot_result.debug,
                }
            )

        for agent in self._agents:
            result = agent.analyze(working_text, request.TenantType)
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
    def _extract_by_expected_slot(request: InferenceRequest, text: str) -> AgentResult | None:
        metadata = request.Metadata or {}
        expected_slot = (metadata.get("expectedSlot") or "").strip()
        if not expected_slot:
            return None

        cleaned = text.strip()
        if not cleaned:
            return None

        dados = DadosExtraidosModel()
        debug: dict[str, object] = {"expectedSlot": expected_slot}

        if expected_slot == "VisitorName":
            candidate = InferenceService._extract_name_only(cleaned)
            if candidate:
                dados.Nome = candidate
                dados.NomeVisitante = candidate
        elif expected_slot in {"ResidentName", "ResidentContext"}:
            resident = InferenceService._extract_resident_name(cleaned)
            unit = InferenceService._extract_pattern(r"(?:apartamento|apto|ap|unidade|sala)\s*(?:n[uú]mero|nº|n°)?\s*([0-9]{1,5}[A-Za-z]?)", cleaned)
            block = InferenceService._extract_pattern(r"\bbloco\s*([A-Za-z0-9]{1,8})\b", cleaned)
            tower = InferenceService._extract_pattern(r"\btorre\s*([A-Za-z0-9]{1,10})\b", cleaned)
            if resident:
                dados.Nome = resident
            if unit:
                dados.Unidade = unit
            if block:
                dados.Bloco = block
            if tower:
                dados.Torre = tower
        elif expected_slot == "Apartment":
            unit = InferenceService._extract_pattern(r"(?:apartamento|apto|ap|unidade|sala)\s*(?:n[uú]mero|nº|n°)?\s*([0-9]{1,5}[A-Za-z]?)", cleaned)
            if not unit:
                bare = re.match(r"^([A-Za-z0-9]{1,10})$", cleaned)
                unit = bare.group(1) if bare else None
            if unit:
                dados.Unidade = unit
            block = InferenceService._extract_pattern(r"\bbloco\s*([A-Za-z0-9]{1,8})\b", cleaned)
            tower = InferenceService._extract_pattern(r"\btorre\s*([A-Za-z0-9]{1,10})\b", cleaned)
            if block:
                dados.Bloco = block
            if tower:
                dados.Torre = tower
        elif expected_slot == "Block":
            block = InferenceService._extract_pattern(r"\bbloco\s*([A-Za-z0-9]{1,8})\b", cleaned) or InferenceService._extract_single_token(cleaned)
            if block:
                dados.Bloco = block
        elif expected_slot == "Tower":
            tower = InferenceService._extract_pattern(r"\btorre\s*([A-Za-z0-9]{1,10})\b", cleaned) or InferenceService._extract_single_token(cleaned)
            if tower:
                dados.Torre = tower
        elif expected_slot == "Document":
            document = InferenceService._extract_document(cleaned)
            if document:
                dados.Documento = document
                if len(document) == 11:
                    dados.Cpf = document

        if not any(value not in (None, False, "") for value in dados.model_dump().values()):
            return None

        return AgentResult(
            agent_name="expected-slot",
            confidence=0.84,
            intent="Identificacao",
            dados=dados,
            debug=debug,
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

    @staticmethod
    def _extract_pattern(pattern: str, text: str) -> str | None:
        match = re.search(pattern, text, flags=re.IGNORECASE)
        if not match:
            return None
        return match.group(1).strip(" .,;:!?\"")

    @staticmethod
    def _extract_name_only(text: str) -> str | None:
        normalized = re.sub(r"\s+", " ", text).strip(" .,;:!?")
        if not normalized or any(char.isdigit() for char in normalized):
            return None
        return normalized

    @staticmethod
    def _extract_resident_name(text: str) -> str | None:
        cleaned = re.sub(r"\b(?:apartamento|apto|ap|unidade|sala|bloco|torre)\b.*$", "", text, flags=re.IGNORECASE).strip(" ,.;:!?")
        cleaned = re.sub(r"\s+", " ", cleaned)
        cleaned = re.sub(r"\b(?:de|da|do|dos|das)\s*$", "", cleaned, flags=re.IGNORECASE).strip(" ,.;:!?")
        if not cleaned:
            return None
        return cleaned

    @staticmethod
    def _extract_single_token(text: str) -> str | None:
        match = re.match(r"^([A-Za-z0-9]{1,10})$", text.strip())
        return match.group(1) if match else None

    @staticmethod
    def _extract_document(text: str) -> str | None:
        digits = re.sub(r"\D", "", text)
        if len(digits) < 4:
            return None
        return digits