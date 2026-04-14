# AIPort NLP Agent Service

Servico Python dedicado a NLP para o ecossistema AIPort.

## Objetivo

Este projeto substitui a camada NLP embarcada no servico `AIPort.Intelligence.Service` por uma API Python orientada a agentes. O fluxo pretendido fica:

- Regex no servico de inteligencia atual
- NLP/NER no servico Python
- LLM continua no servico de inteligencia atual

## API

- `POST /api/inference/process`
- `GET /api/inference/health`
- `GET /openapi/v1.json`
- `GET /v1/scalar`
- `GET /scalar/v1` alias de compatibilidade
- `GET /docs` redireciona para `GET /v1/scalar`

O endpoint principal aceita o mesmo contrato de entrada do servico de inteligencia atual:

```json
{
  "Texto": "meu nome e Carlos, vou para o apartamento 204",
  "TenantType": "residential",
  "SessionId": "abc-123",
  "Metadata": {
    "origem": "orchestrator"
  }
}
```

E devolve uma resposta orientada a NLP para ser consumida pelo pipeline .NET:

```json
{
  "Intencao": "Identificacao",
  "DadosExtraidos": {
    "Nome": "Carlos",
    "NomeVisitante": "Carlos",
    "Documento": null,
    "Cpf": null,
    "Unidade": "204",
    "Bloco": null,
    "Torre": null,
    "Empresa": null,
    "Parentesco": null,
    "EstaComVeiculo": false,
    "Placa": null,
    "EEntregador": false
  },
  "Confianca": 0.86,
  "Camada": "NLP-Agent-Orchestrator",
  "Debug": {
    "agents": ["spaCy", "heuristic"]
  }
}
```

## Agentes

- `spaCy`: tenta NER com modelo configurado
- `heuristic`: cobre fallback com regras e extracao estruturada em portugues

## Configuracao

Variaveis de ambiente suportadas:

- `AIPORT_PY_NLP_SERVICE_NAME`
- `AIPORT_PY_NLP_LOG_LEVEL`
- `AIPORT_PY_NLP_SPACY_ENABLED`
- `AIPORT_PY_NLP_SPACY_MODEL`
- `AIPORT_PY_NLP_HEURISTIC_ENABLED`

## Execucao local

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8010 --reload
```

## Documentacao

Depois de subir o servico localmente:

- `http://localhost:8010/openapi/v1.json`
- `http://localhost:8010/v1/scalar`
- `http://localhost:8010/scalar/v1`
- `http://localhost:8010/docs`