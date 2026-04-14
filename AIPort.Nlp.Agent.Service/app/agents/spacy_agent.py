from __future__ import annotations

import logging

from app.agents.base import AgentResult
from app.models import DadosExtraidosModel

LOGGER = logging.getLogger(__name__)

try:
    import spacy
except Exception:
    spacy = None


class SpacyNlpAgent:
    name = "spaCy"

    def __init__(self, model_name: str) -> None:
        self._model_name = model_name
        self._nlp = None
        self._load_failed = False

    def analyze(self, text: str, tenant_type: str) -> AgentResult:
        nlp = self._ensure_model()
        if nlp is None:
            return AgentResult(agent_name=self.name)

        try:
            doc = nlp(text)
        except Exception as exc:
            LOGGER.warning("spaCy falhou ao processar texto: %s", exc)
            return AgentResult(agent_name=self.name)

        dados = DadosExtraidosModel()
        debug: dict[str, object] = {"tenant": tenant_type, "entities": []}

        for ent in doc.ents:
            label = ent.label_.upper()
            value = ent.text.strip()
            debug["entities"].append({"label": label, "value": value})

            if not value:
                continue

            if label in {"PER", "PERSON"} and not dados.Nome:
                dados.Nome = value
            elif label in {"ORG", "ORGANIZATION"} and not dados.Empresa:
                dados.Empresa = value

        if dados.Nome and any(marker in text.casefold() for marker in ("me chamo", "meu nome", "eu sou", "aqui é")):
            dados.NomeVisitante = dados.Nome

        confidence = 0.0
        if dados.Nome:
            confidence += 0.42
        if dados.Empresa:
            confidence += 0.30

        intent = "Identificacao" if confidence > 0 else None
        return AgentResult(agent_name=self.name, confidence=min(confidence, 0.88), intent=intent, dados=dados, debug=debug)

    def _ensure_model(self):
        if self._nlp is not None:
            return self._nlp

        if self._load_failed or spacy is None:
            return None

        try:
            self._nlp = spacy.load(self._model_name)
        except Exception as exc:
            LOGGER.warning("Nao foi possivel carregar o modelo spaCy '%s': %s", self._model_name, exc)
            self._load_failed = True
            return None

        return self._nlp