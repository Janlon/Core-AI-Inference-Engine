from __future__ import annotations

import re

from app.agents.base import AgentResult
from app.models import DadosExtraidosModel


class HeuristicNlpAgent:
    name = "heuristic"

    _self_name_pattern = re.compile(
        r"(?:me\s+chamo|meu\s+nome\s+[eé]|eu\s+sou|aqui\s+[eé])\s+([A-Za-zÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç]+(?:\s+[A-Za-zÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç]+){0,4})",
        re.IGNORECASE,
    )
    _visit_name_pattern = re.compile(
        r"(?:visitar|falar\s+com|ver)\s+(?:o|a)?\s*([A-Za-zÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç]+(?:\s+[A-Za-zÁÀÂÃÉÊÍÓÔÕÚÇáàâãéêíóôõúç]+){0,4})",
        re.IGNORECASE,
    )
    _unit_pattern = re.compile(r"(?:apartamento|apto|ap|unidade|sala)\s*(?:n[uú]mero|nº|n°)?\s*([0-9]{1,5}[A-Za-z]?)", re.IGNORECASE)
    _block_pattern = re.compile(r"\bbloco\s*([A-Za-z0-9]{1,8})\b", re.IGNORECASE)
    _tower_pattern = re.compile(r"\btorre\s*([A-Za-z0-9]{1,10})\b", re.IGNORECASE)
    _document_pattern = re.compile(r"\b(?:rg|cnh|crm|coren|documento)\s*[:#-]?\s*([A-Za-z0-9.-]{4,20})\b", re.IGNORECASE)
    _cpf_pattern = re.compile(r"\b([0-9]{3}\.?[0-9]{3}\.?[0-9]{3}-?[0-9]{2})\b")
    _company_pattern = re.compile(r"\b([A-Z][A-Za-z0-9&\.\s]{2,40}(?:LTDA|S\.A\.|S/A|ME|EPP|EIRELI))\b")
    _plate_pattern = re.compile(r"\b([A-Z]{3}[0-9][A-Z0-9][0-9]{2})\b", re.IGNORECASE)
    _kinship_pattern = re.compile(r"\b(pai|m[aã]e|irm[aã]o|filho|filha|esposa|esposo|marido|namorada|namorado|tia|tio|prima|primo)\b", re.IGNORECASE)

    _delivery_keywords = ("entrega", "entregador", "motoboy", "ifood", "rappi")
    _vehicle_keywords = ("carro", "ve[ií]culo", "moto")
    _greeting_keywords = ("oi", "ola", "olá", "bom dia", "boa tarde", "boa noite")
    _farewell_keywords = ("tchau", "obrigado", "obrigada", "ate logo", "até logo")
    _emergency_keywords = ("emergencia", "emergência", "socorro", "urgente", "incendio", "incêndio")

    def analyze(self, text: str, tenant_type: str) -> AgentResult:
        normalized = text.strip()
        lowered = normalized.casefold()
        dados = DadosExtraidosModel()
        debug: dict[str, object] = {"tenant": tenant_type}

        self_name = self._first_group(self._self_name_pattern, normalized)
        if self_name:
            dados.Nome = self_name
            dados.NomeVisitante = self_name
            debug["self_name"] = self_name

        resident_name = self._first_group(self._visit_name_pattern, normalized)
        if resident_name and not dados.Nome:
            dados.Nome = resident_name
            debug["target_name"] = resident_name

        if not dados.Nome:
            direct_name = self._extract_direct_name(normalized)
            if direct_name:
                dados.Nome = direct_name
                dados.NomeVisitante = direct_name
                debug["direct_name"] = direct_name

        dados.Unidade = self._first_group(self._unit_pattern, normalized)
        dados.Bloco = self._first_group(self._block_pattern, normalized)
        dados.Torre = self._first_group(self._tower_pattern, normalized)
        dados.Documento = self._first_group(self._document_pattern, normalized)
        dados.Cpf = self._first_group(self._cpf_pattern, normalized)
        dados.Empresa = self._first_group(self._company_pattern, normalized)
        dados.Placa = self._normalize_plate(self._first_group(self._plate_pattern, normalized))
        dados.Parentesco = self._first_group(self._kinship_pattern, normalized)
        dados.EEntregador = any(keyword in lowered for keyword in self._delivery_keywords)
        dados.EstaComVeiculo = any(re.search(keyword, lowered) for keyword in self._vehicle_keywords)

        intent = self._infer_intent(lowered, dados)
        confidence = self._score(dados, intent)

        return AgentResult(
            agent_name=self.name,
            confidence=confidence,
            intent=intent,
            dados=dados,
            debug=debug,
        )

    @staticmethod
    def _first_group(pattern: re.Pattern[str], text: str) -> str | None:
        match = pattern.search(text)
        if not match:
            return None

        return match.group(1).strip(" .,;:!?\"")

    @staticmethod
    def _extract_direct_name(text: str) -> str | None:
        tokens = [token.strip(" .,;:!?") for token in text.split() if token.strip(" .,;:!?")]
        if not tokens or len(tokens) > 4:
            return None

        if any(any(char.isdigit() for char in token) for token in tokens):
            return None

        invalid = {"oi", "ola", "olá", "entrega", "ifood", "rappi", "bom", "boa", "dia", "tarde", "noite"}
        if all(token.casefold() not in invalid for token in tokens):
            return " ".join(tokens)

        return None

    def _infer_intent(self, lowered: str, dados: DadosExtraidosModel) -> str | None:
        if any(keyword in lowered for keyword in self._emergency_keywords):
            return "Urgencia"

        if any(keyword in lowered for keyword in self._farewell_keywords):
            return "Despedida"

        if dados.Nome or dados.NomeVisitante or dados.Unidade or dados.Documento or dados.Cpf or dados.Empresa:
            return "Identificacao"

        if any(keyword in lowered for keyword in self._greeting_keywords):
            return "Saudacao"

        return None

    @staticmethod
    def _score(dados: DadosExtraidosModel, intent: str | None) -> float:
        score = 0.0
        payload = dados.model_dump()
        for key, value in payload.items():
            if value not in (None, False, ""):
                score += 0.08 if key in {"Nome", "NomeVisitante", "Empresa"} else 0.06

        if intent:
            score += 0.18

        if dados.Nome and dados.NomeVisitante:
            score += 0.12

        return min(score, 0.92)

    @staticmethod
    def _normalize_plate(plate: str | None) -> str | None:
        if not plate:
            return None

        return plate.upper()