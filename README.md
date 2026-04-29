# aiport

Repositorio do sistema de portaria virtual com IA.

## Visao geral

O sistema AIPort e uma plataforma de atendimento para portaria virtual. Ele recebe a fala ou o texto do visitante, identifica o contexto da visita, extrai dados relevantes, decide a proxima acao operacional e integra essa decisao com o fluxo de voz, notificacao e orquestracao da chamada.

O objetivo principal e automatizar o atendimento inicial de visitantes, entregadores e situacoes de identificacao, preservando fallback para atendimento humano quando a automacao nao possui confianca suficiente ou quando faltam dados criticos.

## Componentes principais

- `AIPort.Adapter.Orchestrator`: orquestracao da chamada/fluxo operacional
- `AIPort.Intelligence.Service`: pipeline principal de decisao `Regex -> NLP -> LLM`
- `AIPort.Nlp.Agent.Service`: novo servico Python especializado em NLP/agentes, consumido via API pelo servico de inteligencia
- `AIPort.Adapter.Orchestrator.Tests`: testes do orquestrador
- `AIPort.Intelligence.Service.Tests`: testes do motor de inteligencia
- `admin-portal`: interface web administrativa

## Como o sistema funciona

Fluxo resumido de atendimento:

1. O `AIPort.Adapter.Orchestrator` inicia a chamada, toca os prompts de voz e coleta as respostas do visitante.
2. O Orchestrator envia o texto capturado para o `AIPort.Intelligence.Service`.
3. O `AIPort.Intelligence.Service` processa a entrada em camadas:
	Regex para extracao deterministica e muito rapida
	NLP externo em Python para extracao orientada por contexto e slot esperado
	LLM para validacao e decisao final quando necessario
4. O resultado estruturado retorna ao Orchestrator com intencao, entidades extraidas, resposta falada e acao sugerida.
5. O Orchestrator executa a acao final: solicitar mais dados, notificar morador, transferir para humano ou encerrar o fluxo.

## Arquitetura atual da inteligencia

O motor de inteligencia foi dividido para separar responsabilidades:

- `Regex`: extrai padroes claros como documento, unidade, bloco, torre e expressoes fixas.
- `NLP`: roda no `AIPort.Nlp.Agent.Service` e atua como extrator orientado a contexto, especialmente durante slot-filling.
- `LLM`: permanece no `AIPort.Intelligence.Service` e deve ser a camada de validacao semantica final, principalmente quando o fluxo precisa confirmar se os dados coletados estao coerentes.

## Novo desenho da camada NLP

O NLP deixou de ser executado localmente dentro do servico .NET como implementacao principal. Agora o fluxo padrao e:

1. Regex continua no `AIPort.Intelligence.Service`
2. NLP/NER passa pelo `AIPort.Nlp.Agent.Service`
3. LLM continua no `AIPort.Intelligence.Service`

Esse servico Python nao substitui o motor inteiro de IA. Ele substitui especificamente a camada de NLP/NER e extracao contextual.

## Slot-filling residencial

Para cenarios residenciais, o Orchestrator conduz um fluxo guiado de perguntas como:

1. nome do visitante
2. nome do morador
3. apartamento
4. bloco/torre, quando aplicavel
5. documento

Cada rodada envia ao motor de inteligencia:

- o texto atual da resposta
- o `expectedSlot`
- o contexto acumulado da visita

Isso evita que um nome informado na rodada do morador seja tratado como nome do visitante e vice-versa.

Ao final do preenchimento dos slots obrigatorios, o sistema realiza uma validacao final via IA para decidir se os dados estao completos, coerentes e suficientes para a acao final.

## Servicos e portas padrao

- `AIPort.Adapter.Orchestrator`: `http://localhost:5592` em desenvolvimento
- `AIPort.Intelligence.Service`: `http://localhost:5037` em desenvolvimento
- `AIPort.Nlp.Agent.Service`: `http://localhost:8010` em desenvolvimento

## Endpoints relevantes

- `AIPort.Intelligence.Service`: `POST /api/inference/process`
- `AIPort.Intelligence.Service`: `GET /api/inference/responses/{tenantType}`
- `AIPort.Intelligence.Service`: `GET /api/inference/health`
- `AIPort.Nlp.Agent.Service`: `POST /api/inference/process`
- `AIPort.Nlp.Agent.Service`: `GET /api/inference/health`
- `AIPort.Nlp.Agent.Service`: `GET /openapi/v1.json`
- `AIPort.Nlp.Agent.Service`: `GET /v1/scalar`
- `AIPort.Adapter.Orchestrator`: `GET /health`

## Documentacao das APIs

- `AIPort.Adapter.Orchestrator`: `GET /scalar/v1`
- `AIPort.Intelligence.Service`: `GET /scalar/v1`
- `AIPort.Nlp.Agent.Service`: `GET /v1/scalar`

No servico Python tambem existe compatibilidade com `GET /scalar/v1` e `GET /docs`.

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

Para o provider LLM principal em producao, o `AIPort.Intelligence.Service` tambem aceita:

- `AIPORT_AI_LLM_PRIMARY_NAME`
- `AIPORT_AI_LLM_PRIMARY_SERVICE_TYPE`
- `AIPORT_AI_LLM_PRIMARY_API_KEY`
- `AIPORT_AI_LLM_PRIMARY_MODEL`
- `AIPORT_AI_LLM_PRIMARY_DEPLOYMENT_NAME`
- `AIPORT_AI_LLM_PRIMARY_ENDPOINT`
- `AIPORT_AI_LLM_PRIMARY_ENABLED`
- `AIPORT_AI_LLM_PRIMARY_PRIORITY`

No `AIPort.Adapter.Orchestrator`, o sandbox de desenvolvimento aceita configuracoes relevantes em `DeveloperSandbox`, incluindo:

- `DisableWebhookCalls`
- `VerboseConsoleOutput`
- `WindowsVoice.Provider`

## Desenvolvimento local

### 1. Subir o servico Python de NLP

```powershell
Set-Location "C:\repositorio\Core-AI-Inference-Engine-github\Core-AI-Inference-Engine\AIPort.Nlp.Agent.Service"
..\.venv\Scripts\python.exe -m pip install -r requirements.txt
..\.venv\Scripts\python.exe -m uvicorn app.main:app --host 0.0.0.0 --port 8010 --reload
```

### 2. Subir o servico de inteligencia

```powershell
Set-Location "C:\repositorio\Core-AI-Inference-Engine-github\Core-AI-Inference-Engine\AIPort.Intelligence.Service"
dotnet run
```

### 3. Subir o orquestrador

```powershell
Set-Location "C:\repositorio\Core-AI-Inference-Engine-github\Core-AI-Inference-Engine\AIPort.Adapter.Orchestrator"
dotnet run
```

### 4. Opcional: ajustar o console do Windows para UTF-8

```powershell
chcp 65001
```

## Execucao local

Suba primeiro o servico Python em `http://localhost:8010`, depois o `AIPort.Intelligence.Service` e por fim o `AIPort.Adapter.Orchestrator`.

## Producao Debian

No Debian, o deploy continua usando o `appsettings.json` padrao de cada servico. Quando necessario, os ajustes de host, credenciais e endpoints podem ser feitos por variaveis de ambiente, sem depender de um `appsettings.Production.json` separado.

O arquivo central recomendado para isso e [aiport.env](c:/repositorio/Core-AI-Inference-Engine-github/Core-AI-Inference-Engine/aiport.env), que agrupa as variaveis dos dois servicos em um unico lugar.

Variaveis minimas recomendadas no Debian:

- `ASPNETCORE_ENVIRONMENT=Production`
- `AIPORT_AI_NLP_EXTERNAL_API_BASE_URL=http://127.0.0.1:8010`
- `AIPORT_AI_LLM_PRIMARY_API_KEY=<sua_chave_gemini>`
- `AIPORT_AI_LLM_PRIMARY_MODEL=gemini-2.5-flash`
- `AIPORT_AI_LLM_PRIMARY_ENDPOINT=https://generativelanguage.googleapis.com/v1beta/openai/`
- `AIPORT_AI_LLM_PRIMARY_SERVICE_TYPE=GoogleAI`
- `AIPORT_AI_LLM_PRIMARY_ENABLED=true`
- `AIPORT_AI_LLM_PRIMARY_PRIORITY=1`

Exemplo de export antes de iniciar o servico:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export AIPORT_AI_NLP_EXTERNAL_API_BASE_URL=http://127.0.0.1:8010
export AIPORT_AI_LLM_PRIMARY_API_KEY="sua-chave-gemini"
export AIPORT_AI_LLM_PRIMARY_MODEL=gemini-2.5-flash
export AIPORT_AI_LLM_PRIMARY_ENDPOINT=https://generativelanguage.googleapis.com/v1beta/openai/
export AIPORT_AI_LLM_PRIMARY_SERVICE_TYPE=GoogleAI
export AIPORT_AI_LLM_PRIMARY_ENABLED=true
export AIPORT_AI_LLM_PRIMARY_PRIORITY=1
./AIPort.Intelligence.Service
```

Para o Orchestrator, o mesmo principio vale: o servico sobe com o `appsettings.json` padrao e os pontos sensiveis de producao ficam em variaveis de ambiente, como `InputSourceMode=Asterisk`, URL do Intelligence Service e credenciais externas.

Variaveis minimas recomendadas no Debian para o Orchestrator:

- `ASPNETCORE_ENVIRONMENT=Production`
- `AIPORT_INPUT_SOURCE_MODE=Asterisk`
- `AIPORT_INTELLIGENCE_BASE_URL=http://127.0.0.1:5005`
- `AIPORT_MARIADB_CONNECTION_STRING=<connection_string_do_mariadb>`
- `AIPORT_ORCHESTRATOR_SERVER_URLS=http://0.0.0.0:5000`
- `AIPORT_GOOGLE_CREDENTIALS_PATH=/opt/aiport/credentials/google_cloud_auth.json`

Exemplo de export antes de iniciar o Orchestrator:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export AIPORT_INPUT_SOURCE_MODE=Asterisk
export AIPORT_INTELLIGENCE_BASE_URL=http://127.0.0.1:5005
export AIPORT_MARIADB_CONNECTION_STRING="Server=127.0.0.1;Port=3306;Database=aiport;User ID=aiport;Password=senha-segura;"
export AIPORT_ORCHESTRATOR_SERVER_URLS=http://0.0.0.0:5000
export AIPORT_GOOGLE_CREDENTIALS_PATH=/opt/aiport/credentials/google_cloud_auth.json
./AIPort.Adapter.Orchestrator
```

Arquivos `systemd` de exemplo foram adicionados em [deploy/systemd/aiport-intelligence.service](c:/repositorio/Core-AI-Inference-Engine-github/Core-AI-Inference-Engine/deploy/systemd/aiport-intelligence.service) e [deploy/systemd/aiport-orchestrator.service](c:/repositorio/Core-AI-Inference-Engine-github/Core-AI-Inference-Engine/deploy/systemd/aiport-orchestrator.service). Ambos leem o mesmo arquivo `/etc/aiport/aiport.env`.

Passos sugeridos no Debian:

```bash
sudo mkdir -p /etc/aiport
sudo cp aiport.env /etc/aiport/aiport.env
sudo cp deploy/systemd/aiport-intelligence.service /etc/systemd/system/
sudo cp deploy/systemd/aiport-orchestrator.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable aiport-intelligence aiport-orchestrator
sudo systemctl start aiport-intelligence
sudo systemctl start aiport-orchestrator
sudo systemctl status aiport-intelligence aiport-orchestrator
```

## Testes

- testes .NET: `dotnet test`
- testes Python do NLP: `pytest AIPort.Nlp.Agent.Service/tests -q`

## Observacoes operacionais

- O arquivo `appsettings.Development.json` do Orchestrator foi ajustado para reduzir ruido de logs em desenvolvimento.
- O sandbox agora pode ocultar transcript detalhado do console via `DeveloperSandbox:VerboseConsoleOutput`.
- O NLP Python foi desenhado para extracao contextual orientada por slot, enquanto a decisao final continua sendo responsabilidade da camada LLM no servico de inteligencia.
