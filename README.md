# aiport

Repositorio do sistema de portaria virtual com IA.

## Componentes principais

- `AIPort.Adapter.Orchestrator`: orquestracao da chamada/fluxo operacional
- `AIPort.Intelligence.Service`: pipeline principal de decisao `Regex -> NLP -> LLM`
- `AIPort.Nlp.Agent.Service`: novo servico Python especializado em NLP/agentes, consumido via API pelo servico de inteligencia

## Novo desenho da camada NLP

O NLP deixou de ser executado localmente dentro do servico .NET como implementacao principal. Agora o fluxo padrao e:

1. Regex continua no `AIPort.Intelligence.Service`
2. NLP/NER passa pelo `AIPort.Nlp.Agent.Service`
3. LLM continua no `AIPort.Intelligence.Service`

## Endpoints relevantes

- `AIPort.Intelligence.Service`: `POST /api/inference/process`
- `AIPort.Nlp.Agent.Service`: `POST /api/inference/process`
- `AIPort.Nlp.Agent.Service`: `GET /api/inference/health`

## Configuracao de integracao

No `AIPort.Intelligence.Service`, a secao `AIService:Nlp` passou a aceitar:

- `UseExternalApi`
- `ExternalApiBaseUrl`
- `ExternalApiTimeoutMs`
- `ExternalApiKey`

Variaveis de ambiente equivalentes:

- `AIPORT_AI_NLP_USE_EXTERNAL_API`
- `AIPORT_AI_NLP_EXTERNAL_API_BASE_URL`
- `AIPORT_AI_NLP_EXTERNAL_API_TIMEOUT_MS`
- `AIPORT_AI_NLP_EXTERNAL_API_KEY`

## Execucao local

Suba primeiro o servico Python em `http://localhost:8010` e depois o `AIPort.Intelligence.Service`.
