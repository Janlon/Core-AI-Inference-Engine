import { useEffect, useMemo, useState } from 'react'
import {
  Activity,
  Building2,
  Database,
  FileClock,
  HeartPulse,
  PhoneCall,
  Server,
  ShieldCheck,
} from 'lucide-react'
import { buildRobotMood } from './components/HealthSentinelCard'
import { HealthTab } from './components/HealthTab'
import { ClientsTab } from './components/ClientsTab'
import { LogsTab } from './components/LogsTab'
import { SummaryCards } from './components/SummaryCards'
import { AppHeader } from './components/AppHeader'
import { BottomNav } from './components/BottomNav'
import {
  API_BASE,
  useClientsList,
  useHealthSnapshot,
  useLiveEventsSSE,
  useRecentEvents,
} from './hooks/usePortalData'
import {
  formatStatus,
  prettyBytes,
  prettyDateTime,
  prettyMs,
  prettyPercent,
  resourceStatus,
} from './lib/portalFormatters'
import { useClientManagement } from './hooks/useClientManagement'
import { useConversationManagement } from './hooks/useConversationManagement'

const tabs = [
  { id: 'health', label: 'Saude', icon: HeartPulse },
  { id: 'clients', label: 'Clientes', icon: Building2 },
  { id: 'logs', label: 'Eventos', icon: FileClock },
]

function App() {
  const [activeTab, setActiveTab] = useState('health')
  const [telemetryHistory, setTelemetryHistory] = useState([])

  const healthQuery = useHealthSnapshot()
  const recentEventsQuery = useRecentEvents()
  const { events: sseEvents, isConnected: sseConnected } = useLiveEventsSSE()
  const clientsQuery = useClientsList()
  const {
    conversationFilters,
    conversationsQuery,
    visibleConversationSessions,
    serverRiskLabel,
    handleConversationFilterChange,
    handleConversationSearch,
    handleConversationPage,
    handleToggleHighRisk,
    handleExportConversationsJson,
    handleExportConversationsCsv,
  } = useConversationManagement()
  const {
    showClientForm,
    editingClient,
    formData,
    closeClientForm,
    handleNewClient,
    handleEditClient,
    handleDeleteClient,
    handleSaveClient,
    handleFormChange,
  } = useClientManagement({ apiBase: API_BASE, clientsQuery })

  const health = healthQuery.data ?? {}
  const dbHealth = health.database ?? {}
  const aiHealth = health.ai ?? {}
  const speechHealth = health.speech ?? {}
  const asteriskHealth = health.asterisk ?? {}
  const systemHealth = health.system ?? {}
  const llmHealth = aiHealth.llm ?? {}
  const cpuHealth = systemHealth.cpu ?? {}
  const memoryHealth = systemHealth.memory ?? {}
  const activeCalls = health.activeCalls ?? asteriskHealth.activeCalls ?? 0
  const latencyMs = aiHealth.latencyMs ?? null
  const cpuUsagePercent = cpuHealth.usagePercent ?? null
  const memoryUsagePercent = memoryHealth.usagePercent ?? null
  const robotMoodState = useMemo(
    () => buildRobotMood({
      healthQueryError: healthQuery.isError,
      overallStatus: health.overall,
      aiStatus: aiHealth.status,
      dbStatus: dbHealth.status,
      speechStatus: speechHealth.status,
      asteriskStatus: asteriskHealth.status,
      sseConnected,
    }),
    [
      aiHealth.status,
      asteriskHealth.status,
      dbHealth.status,
      health.overall,
      healthQuery.isError,
      speechHealth.status,
      sseConnected,
    ],
  )

  useEffect(() => {
    if (!systemHealth.sampledAtUtc) return

    setTelemetryHistory((prev) => {
      const sampleId = String(systemHealth.sampledAtUtc)
      if (prev.some((item) => item.id === sampleId)) {
        return prev
      }

      const next = [
        ...prev,
        {
          id: sampleId,
          at: systemHealth.sampledAtUtc,
          cpuUsagePercent,
          memoryUsagePercent,
        },
      ]

      return next.slice(-24)
    })
  }, [cpuUsagePercent, memoryUsagePercent, systemHealth.sampledAtUtc])

  const realtimeEvents = useMemo(() => {
    const all = [...sseEvents]
    const deduped = []
    const seen = new Set()

    for (const item of all) {
      const key = item.id ?? `${item.at}-${item.message}`
      if (!seen.has(key)) {
        seen.add(key)
        deduped.push(item)
      }
    }

    return deduped
      .sort((left, right) => new Date(right.at ?? right.At) - new Date(left.at ?? left.At))
      .slice(0, 50)
  }, [sseEvents])

  const recentEvents = useMemo(() => {
    return [...(recentEventsQuery.data ?? [])]
      .sort((left, right) => new Date(right.at ?? right.At) - new Date(left.at ?? left.At))
      .slice(0, 20)
  }, [recentEventsQuery.data])

  const topSummary = useMemo(
    () => [
      { label: 'Overall', value: formatStatus(health.overall), icon: ShieldCheck },
      { label: 'Chamadas Ativas', value: String(activeCalls), icon: PhoneCall },
      { label: 'Latencia IA', value: prettyMs(latencyMs), icon: Activity },
      { label: 'Voz/TTS', value: formatStatus(speechHealth.status), icon: HeartPulse },
      { label: 'CPU Host', value: prettyPercent(cpuUsagePercent), icon: Server },
      { label: 'Memoria Host', value: prettyPercent(memoryUsagePercent), icon: Database },
    ],
    [activeCalls, cpuUsagePercent, health.overall, latencyMs, memoryUsagePercent, speechHealth.status],
  )

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-6xl px-4 pb-28 pt-6 sm:px-6 lg:px-8">
        <AppHeader />

        <SummaryCards items={topSummary} />

        {activeTab === 'health' && (
          <HealthTab
            robotMoodState={robotMoodState}
            activeCalls={activeCalls}
            sseConnected={sseConnected}
            latencyMs={latencyMs}
            aiHealth={aiHealth}
            health={health}
            asteriskHealth={asteriskHealth}
            dbHealth={dbHealth}
            cpuUsagePercent={cpuUsagePercent}
            systemHealth={systemHealth}
            speechHealth={speechHealth}
            memoryUsagePercent={memoryUsagePercent}
            memoryHealth={memoryHealth}
            healthQuery={healthQuery}
            llmHealth={llmHealth}
            telemetryHistory={telemetryHistory}
          />
        )}

        {activeTab === 'clients' && (
          <ClientsTab
            clients={clientsQuery.data ?? []}
            clientsQuery={clientsQuery}
            onEdit={handleEditClient}
            onDelete={handleDeleteClient}
            onNew={handleNewClient}
            showClientForm={showClientForm}
            editingClient={editingClient}
            formData={formData}
            onCloseForm={closeClientForm}
            onSubmitForm={handleSaveClient}
            onChangeForm={handleFormChange}
          />
        )}

        {activeTab === 'logs' && (
          <LogsTab
            sseConnected={sseConnected}
            realtimeEvents={realtimeEvents}
            recentEvents={recentEvents}
            clients={clientsQuery.data ?? []}
            conversationFilters={conversationFilters}
            conversationsQuery={conversationsQuery}
            serverRiskLabel={serverRiskLabel}
            visibleConversationSessions={visibleConversationSessions}
            onFilterChange={handleConversationFilterChange}
            onSearch={handleConversationSearch}
            onToggleHighRisk={handleToggleHighRisk}
            onPreviousPage={() => handleConversationPage('previous')}
            onNextPage={() => handleConversationPage('next')}
            onExportJson={handleExportConversationsJson}
            onExportCsv={handleExportConversationsCsv}
          />
        )}
      </div>

      <BottomNav tabs={tabs} activeTab={activeTab} onChange={setActiveTab} />
    </div>
  )
}

export default App
