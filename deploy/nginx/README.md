# Nginx do admin-portal

Este arquivo publica o frontend na porta `81` e encaminha `/api/*` para o orquestrador local em `127.0.0.1:5000`.

## Caminhos esperados

- Frontend buildado em `/opt/aiport/Core_AI_Inference_Engine/admin-portal/dist`
- Orquestrador ouvindo em `127.0.0.1:5000`

## Instalação

1. Copie `admin-portal.conf` para `/etc/nginx/sites-available/admin-portal.conf`.
2. Crie o link simbólico em `/etc/nginx/sites-enabled/admin-portal.conf`.
3. Teste a configuração com `nginx -t`.
4. Recarregue com `systemctl reload nginx`.

## Ajuste do frontend

Quando este proxy estiver ativo, o frontend deve usar mesmo host para a API.
Defina `VITE_API_BASE_URL=` vazio, ou remova a variável do `.env`, depois gere novo build do admin-portal.

## Verificação rápida

- `curl http://127.0.0.1:81/`
- `curl http://127.0.0.1:81/api/health`
- `curl -N http://127.0.0.1:81/api/events/stream`