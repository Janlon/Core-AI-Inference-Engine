# Health Check do Orquestrador

## Visão Geral

O serviço de orquestração agora possui uma capacidade completa de verificação de saúde, permitindo monitorar a disponibilidade da aplicação e, mais importante, **validar se existe uma comunicação ativa com o banco de dados MariaDB**.

## Componentes Implementados

### 1. Interface: `IHealthCheckService`
**Localização:** `Services/Interfaces/IHealthCheckService.cs`

Define o contrato para verificação de saúde com dois métodos principais:
- `IsDatabaseHealthyAsync()` - Verifica especificamente a conexão com o banco de dados
- `GetHealthStatusAsync()` - Obtém o status completo do serviço

### 2. Implementação: `HealthCheckService`
**Localização:** `Services/HealthCheckService.cs`

Implementação concreta que:
- Testa a conexão com MariaDB abrindo e fechando uma conexão
- Captura exceções de timeout ou falha de conectividade
- Retorna status estruturado com timestamp
- Registra logs de sucesso e falha para auditoria

### 3. Controller: `HealthController`
**Localização:** `Controllers/HealthController.cs`

Expõe 3 endpoints HTTP para monitoramento:

#### **GET /api/health/status**
Retorna o status geral completo do serviço

**Resposta (200 OK):**
```json
{
  "service": "AIPort.Adapter.Orchestrator",
  "timestamp": "2026-03-17T10:30:45.1234567Z",
  "version": "1.0.0",
  "system": {
    "status": "healthy",
    "platform": "linux",
    "sampledAtUtc": "2026-04-08T10:30:45.1234567Z",
    "logicalCores": 8,
    "cpu": {
      "usagePercent": 17.2
    },
    "memory": {
      "totalBytes": 16777216000,
      "usedBytes": 7340032000,
      "availableBytes": 9437184000,
      "usagePercent": 43.8
    },
    "message": "Telemetria coletada do host Linux via /proc."
  },
  "database": {
    "status": "healthy",
    "timestamp": "2026-03-17T10:30:45.1234567Z"
  },
  "overall": "healthy"
}
```

No Linux, incluindo Debian 13, a telemetria de CPU e memoria e obtida de `/proc/stat` e `/proc/meminfo`.

#### **GET /api/health/database**
Verifica **especificamente a conexão com o banco de dados**

**Resposta quando saudável (200 OK):**
```json
{
  "status": "healthy",
  "component": "database",
  "timestamp": "2026-03-17T10:30:45.1234567Z"
}
```

**Resposta quando indisponível (503 Service Unavailable):**
```json
{
  "status": "unhealthy",
  "component": "database",
  "message": "Falha ao conectar ao banco de dados MariaDB",
  "timestamp": "2026-03-17T10:30:45.1234567Z"
}
```

#### **GET /api/health/live**
Verifica se o serviço está respondendo (liveness check)

**Resposta (200 OK):**
```json
{
  "status": "alive",
  "service": "AIPort.Adapter.Orchestrator",
  "timestamp": "2026-03-17T10:30:45.1234567Z"
}
```

### 4. Endpoint Global
**GET /health** - Endpoint simples que retorna o status completo

Mantém compatibilidade com ferramentas de health check genéricas.

## Como Usar

### Verificar Conectividade com Banco de Dados

**Via cURL:**
```bash
curl http://localhost:5292/api/health/database
```

**Via PowerShell:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5292/api/health/database" -Method Get
```

**Via C# HttpClient:**
```csharp
using var client = new HttpClient();
var response = await client.GetAsync("http://localhost:5292/api/health/database");
var content = await response.Content.ReadAsStringAsync();
Console.WriteLine($"Status: {response.StatusCode}");
Console.WriteLine($"Body: {content}");
```

### Injetar o Serviço em Controladores

```csharp
public class SeuController : ControllerBase
{
    private readonly IHealthCheckService _healthCheck;
    
    public SeuController(IHealthCheckService healthCheck)
    {
        _healthCheck = healthCheck;
    }
    
    [HttpGet("meu-metodo")]
    public async Task<IActionResult> MeuMetodo()
    {
        // Verificar se o banco está disponível antes de usar
        if (!await _healthCheck.IsDatabaseHealthyAsync())
        {
            return StatusCode(503, "Banco de dados indisponível");
        }
        
        // Sua lógica aqui
        return Ok();
    }
}
```

### Injetar em Serviços de Domínio

```csharp
public class MeuServico : IMeuServico
{
    private readonly IHealthCheckService _healthCheck;
    
    public MeuServico(IHealthCheckService healthCheck)
    {
        _healthCheck = healthCheck;
    }
    
    public async Task ProcessarAsync()
    {
        if (!await _healthCheck.IsDatabaseHealthyAsync())
            throw new InvalidOperationException("Base de dados indisponível");
        
        // Prosseguir com lógica que depende de DB
    }
}
```

## Comportamento de Erro

O `HealthCheckService` trata os seguintes cenários:

| Cenário | Retorno | Status HTTP |
|---------|---------|------------|
| Conexão bem-sucedida | `true` | 200 |
| Timeout de conexão | `false` | 503 |
| Credenciais inválidas | `false` | 503 |
| Host não disponível | `false` | 503 |
| Operação cancelada | `false` | 503 |
| Qualquer outra exceção | `false` + log | 503 |

## Logging

Todos os eventos são registrados via `ILogger<HealthCheckService>`:

```
info: AIPort.Adapter.Orchestrator.Services.HealthCheckService[0]
      Conexão com banco de dados verificada com sucesso

warn: AIPort.Adapter.Orchestrator.Services.HealthCheckService[0]
      Verificação de conexão com banco de dados foi cancelada

err: AIPort.Adapter.Orchestrator.Services.HealthCheckService[0]
      Falha ao verificar conexão com banco de dados: Connection timeout
```

## Configuração

O serviço é registrado automaticamente no `Program.cs`:

```csharp
builder.Services.AddScoped<IHealthCheckService, HealthCheckService>();
```

A string de conexão é obtida da configuração via `IDbConnectionFactory`, que usa as variáveis de ambiente:
- `AIPORT_MARIADB_CONNECTION_STRING` (Debian)
- Ou `MariaDb:ConnectionString` (appsettings.json)

## Exemplo de Monitoramento em Produção

Para monitorar continuamente em produção, você pode:

1. **Kubernetes liveness probe:**
```yaml
livenessProbe:
  httpGet:
    path: /api/health/database
    port: 5292
  initialDelaySeconds: 10
  periodSeconds: 30
```

2. **Systemd healthcheck:** Adicionar verificação periódica no script de startup

3. **Prometheus/Grafana:** Expor endpoint de health em `/metrics` (extensível)

## Segurança

- ✅ Nenhuma informação sensível exposta (credenciais não aparecem)
- ✅ Apenas valida conectividade, não expõe estado interno
- ✅ Avisos de erro contêm descrição técnica mas ainda genérica
- ✅ Logs detalhados apenas em nível `Debug` ou `Information` controlado

## Próximas Melhorias (Opcional)

Para versões futuras, considere:
- [ ] Adicionar timeout configurável para testes de DB
- [ ] Incluir outras dependências (Redis, APIs externas)
- [ ] Métricas Prometheus (`/metrics`)
- [ ] Dashboard de health em tempo real
- [ ] Alertas automáticos para queda de conectividade
