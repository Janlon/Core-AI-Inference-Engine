import { Activity, Database, HeartPulse, Network, PhoneCall, Server } from 'lucide-react'
import { HealthSentinelCard } from './HealthSentinelCard'
import { HealthCard } from './HealthCard'
import { LlmProvidersPanel } from './LlmProvidersPanel'
import { TelemetryChart } from './TelemetryChart'
import { prettyBytes, prettyDateTime, prettyMs, prettyPercent, resourceStatus } from '../lib/portalFormatters'

export function HealthTab({
  robotMoodState,
  activeCalls,
  sseConnected,
  latencyMs,
  aiHealth,
  health,
  asteriskHealth,
  dbHealth,
  cpuUsagePercent,
  systemHealth,
  speechHealth,
  memoryUsagePercent,
  memoryHealth,
  healthQuery,
  llmHealth,
  telemetryHistory,
}) {
  return (
    <main className="space-y-5">
      <HealthSentinelCard
        mood={robotMoodState.mood}
        title={robotMoodState.title}
        subtitle={robotMoodState.subtitle}
        badgeClass={robotMoodState.badgeClass}
        activeCalls={activeCalls}
        sseConnected={sseConnected}
        latencyMs={latencyMs}
        aiStatus={aiHealth.status}
        overallStatus={health.overall}
      />

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <HealthCard
          title="Asterisk"
          status={asteriskHealth.status}
          detail={`FastAGI ${asteriskHealth.host ?? '--'}:${asteriskHealth.port ?? '--'} • ${asteriskHealth.activeCalls ?? 0} chamadas`}
          icon={PhoneCall}
        />
        <HealthCard
          title="MariaDB"
          status={dbHealth.status}
          detail={`${dbHealth.activeTenantsCount ?? '--'} tenants ativos`}
          icon={Database}
        />
        <HealthCard
          title="Servico de IA"
          status={aiHealth.status}
          detail={`Base URL: ${aiHealth.baseUrl ?? '--'}`}
          icon={Server}
        />
        <HealthCard
          title="Rede"
          status={latencyMs == null ? 'unknown' : 'healthy'}
          detail={`Latencia probe IA: ${prettyMs(latencyMs)}`}
          icon={Network}
        />
        <HealthCard
          title="Voz / TTS"
          status={speechHealth.status}
          detail={`${speechHealth.provider ?? '--'} • ${speechHealth.ready ? 'pronto' : 'nao pronto'}`}
          icon={HeartPulse}
        />
        <HealthCard
          title="CPU Host"
          status={resourceStatus(cpuUsagePercent, systemHealth.status)}
          detail={`${prettyPercent(cpuUsagePercent)} de uso • ${systemHealth.logicalCores ?? '--'} cores`}
          icon={Activity}
        />
        <HealthCard
          title="Memoria Host"
          status={resourceStatus(memoryUsagePercent, systemHealth.status)}
          detail={`${prettyBytes(memoryHealth.usedBytes)} / ${prettyBytes(memoryHealth.totalBytes)}`}
          icon={Database}
        />
      </section>

      <section className="glass-card">
        <p className="text-sm text-slate-400">Atualizacao automatica</p>
        <p className="mt-1 text-sm text-slate-200">O health check e consultado a cada 30 segundos em <span className="font-mono text-cyan-300">/api/health</span>.</p>
        <p className="mt-2 text-xs text-slate-400">Em Linux, incluindo Debian 13, CPU e memoria sao lidos do host via /proc/stat e /proc/meminfo.</p>
        <p className="mt-1 text-xs text-slate-500">Ultima amostra: {systemHealth.sampledAtUtc ? new Date(systemHealth.sampledAtUtc).toLocaleString() : '--'}.</p>
        <p className="mt-1 text-xs text-slate-500">Voz: {speechHealth.ready ? 'pronta para atender' : 'ainda aquecendo'} desde {prettyDateTime(speechHealth.lastWarmupAtUtc)}.</p>
        {speechHealth.lastWarmupElapsedMs != null && <p className="mt-1 text-xs text-slate-500">Warmup TTS: {prettyMs(speechHealth.lastWarmupElapsedMs)}.</p>}
        {speechHealth.message && <p className="mt-1 text-xs text-slate-500">{speechHealth.message}</p>}
        {systemHealth.message && <p className="mt-1 text-xs text-slate-500">{systemHealth.message}</p>}
        {healthQuery.isFetching && <p className="mt-2 text-xs text-cyan-300">Atualizando status...</p>}
        {healthQuery.isError && <p className="mt-2 text-xs text-rose-300">Nao foi possivel consultar o backend agora.</p>}
      </section>

      <LlmProvidersPanel llm={llmHealth} />

      <section className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <TelemetryChart
          title="CPU em tempo real"
          valueLabel={prettyPercent(cpuUsagePercent)}
          subtitle="Historico local alimentado a cada refresh do health check"
          series={telemetryHistory.map((item) => ({ id: `${item.id}-cpu`, at: item.at, value: item.cpuUsagePercent }))}
          strokeClass="stroke-cyan-400 fill-cyan-400"
          fillClass="fill-cyan-500/15"
        />
        <TelemetryChart
          title="Memoria em tempo real"
          valueLabel={prettyPercent(memoryUsagePercent)}
          subtitle={`${prettyBytes(memoryHealth.usedBytes)} em uso de ${prettyBytes(memoryHealth.totalBytes)}`}
          series={telemetryHistory.map((item) => ({ id: `${item.id}-memory`, at: item.at, value: item.memoryUsagePercent }))}
          strokeClass="stroke-emerald-400 fill-emerald-400"
          fillClass="fill-emerald-500/15"
        />
      </section>
    </main>
  )
}