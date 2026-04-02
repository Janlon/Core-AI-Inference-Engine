import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  Activity,
  Building2,
  ChevronDown,
  ChevronUp,
  Database,
  Edit2,
  FileClock,
  HeartPulse,
  Network,
  PhoneCall,
  Server,
  ShieldCheck,
  Trash2,
  X,
  Plus,
} from 'lucide-react'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

const tabs = [
  { id: 'health', label: 'Saude', icon: HeartPulse },
  { id: 'clients', label: 'Clientes', icon: Building2 },
  { id: 'logs', label: 'Eventos', icon: FileClock },
]

async function fetchJson(url) {
  const response = await fetch(url)
  if (!response.ok) throw new Error(`Erro ${response.status} em ${url}`)
  return response.json()
}

function formatStatus(status) {
  if (!status) return 'desconhecido'
  return String(status).toLowerCase()
}

function statusTone(status) {
  const normalized = formatStatus(status)
  if (normalized === 'healthy' || normalized === 'alive' || normalized === 'ok') return 'ok'
  if (normalized === 'degraded' || normalized === 'warning') return 'warn'
  return 'down'
}

function prettyMs(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'
  return `${Math.round(Number(value))} ms`
}

function toUtcIsoFromLocalInput(localValue, inclusiveEnd = false) {
  if (!localValue) return null

  const [datePart, timePartRaw] = String(localValue).split('T')
  if (!datePart || !timePartRaw) return null

  const [yearStr, monthStr, dayStr] = datePart.split('-')
  const [hourStr = '0', minuteStr = '0', secondStr = '0'] = timePartRaw.split(':')

  const year = Number(yearStr)
  const month = Number(monthStr)
  const day = Number(dayStr)
  const hour = Number(hourStr)
  const minute = Number(minuteStr)
  const second = Number(secondStr)
  const millisecond = 0

  if ([year, month, day, hour, minute, second, millisecond].some((n) => Number.isNaN(n))) return null

  const date = new Date(year, month - 1, day, hour, minute, second, millisecond)
  if (inclusiveEnd) {
    date.setSeconds(59, 999)
  }

  return date.toISOString()
}

function useHealthSnapshot() {
  return useQuery({
    queryKey: ['health'],
    queryFn: async () => fetchJson(`${API_BASE}/api/health`),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  })
}

function useClientsList() {
  return useQuery({
    queryKey: ['clients'],
    queryFn: async () => fetchJson(`${API_BASE}/api/tenants?includeInactive=true`),
    refetchInterval: 30_000,
  })
}

function useRecentEvents() {
  return useQuery({
    queryKey: ['events', 'recent'],
    queryFn: async () => {
      const payload = await fetchJson(`${API_BASE}/api/events?limit=20`)
      return payload.events ?? []
    },
    refetchInterval: false,
  })
}

function useConversationThreads(filters) {
  return useQuery({
    queryKey: ['events', 'conversations', filters],
    queryFn: async () => {
      const params = new URLSearchParams()

      if (filters.tenantId) params.set('tenantId', filters.tenantId)
      if (filters.keyword) params.set('keyword', filters.keyword)
      if (filters.sessionId) params.set('sessionId', filters.sessionId)
      if (filters.loopRisk) params.set('loopRisk', filters.loopRisk)
      const fromUtcIso = toUtcIsoFromLocalInput(filters.fromUtc, false)
      const toUtcIso = toUtcIsoFromLocalInput(filters.toUtc, true)
      if (fromUtcIso) params.set('fromUtc', fromUtcIso)
      if (toUtcIso) params.set('toUtc', toUtcIso)
      params.set('limit', String(Number(filters.limit ?? 20)))
      params.set('offset', String(Number(filters.offset ?? 0)))

      return fetchJson(`${API_BASE}/api/events/conversations?${params.toString()}`)
    },
    refetchInterval: 45_000,
  })
}

function useLiveEventsSSE() {
  const [events, setEvents] = useState([])
  const [isConnected, setIsConnected] = useState(false)

  useEffect(() => {
    let eventSource = null
    let reconnectTimer = null
    let isSubscribed = true

    const connect = () => {
      try {
        eventSource = new EventSource(`${API_BASE}/api/events/stream`)

        eventSource.addEventListener('open', () => {
          if (isSubscribed) {
            setIsConnected(true)
          }
        })

        eventSource.addEventListener('message', (event) => {
          if (isSubscribed) {
            try {
              const data = JSON.parse(event.data)
              if (data.type === 'connected') {
                return
              }

              setEvents((prev) => {
                const alreadyExists = prev.some((item) => item.id === data.id)
                if (alreadyExists) {
                  return prev
                }

                return [data, ...prev].slice(0, 50)
              })
            } catch (e) {
              console.error('Erro ao parsear evento SSE:', e)
            }
          }
        })

        eventSource.addEventListener('error', () => {
          if (isSubscribed) {
            setIsConnected(false)
            eventSource?.close()
            // Tenta reconectar em 3s
            reconnectTimer = setTimeout(connect, 3_000)
          }
        })
      } catch (e) {
        console.error('Erro ao conectar ao SSE:', e)
        if (isSubscribed) {
          reconnectTimer = setTimeout(connect, 3_000)
        }
      }
    }

    connect()

    return () => {
      isSubscribed = false
      eventSource?.close()
      if (reconnectTimer) clearTimeout(reconnectTimer)
    }
  }, [])

  return { events, isConnected }
}

function HealthCard({ title, status, detail, icon: Icon }) {
  const tone = statusTone(status)
  const toneClass = tone === 'ok' ? 'bg-emerald-500' : tone === 'warn' ? 'bg-amber-500' : 'bg-rose-500'
  return (
    <article className="glass-card">
      <div className="mb-4 flex items-start justify-between">
        <div>
          <p className="text-sm text-slate-400">{title}</p>
          <p className="mt-1 text-lg font-semibold capitalize text-slate-100">{formatStatus(status)}</p>
        </div>
        <Icon className="h-5 w-5 text-cyan-300" />
      </div>
      <div className="mt-3 flex items-center gap-2 text-sm text-slate-400">
        <span className={`h-2.5 w-2.5 rounded-full ${toneClass}`} />
        <span>{detail}</span>
      </div>
    </article>
  )
}

function ClientsList({ clients, onEdit, onDelete, onNew }) {
  return (
    <section className="glass-card">
      <div className="mb-4 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-base font-semibold">
          <Building2 className="h-5 w-5 text-cyan-300" />
          Clientes Cadastrados ({clients.length})
        </h3>
        <button
          onClick={onNew}
          className="inline-flex items-center gap-2 rounded-lg bg-cyan-600 px-3 py-1.5 text-xs font-medium text-slate-100 hover:bg-cyan-500 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Novo
        </button>
      </div>
      <div className="space-y-2">
        {clients.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-400">Nenhum cliente cadastrado.</p>
        ) : (
          clients.map((client) => (
            <article key={client.id} className="rounded-xl border border-slate-800 bg-slate-900/40 p-3 hover:border-slate-700 transition-colors">
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-slate-100 truncate">{client.nomeIdentificador}</p>
                  <p className="text-xs text-slate-400 truncate">PID {client.pid} • {client.tipoLocal} • {client.systemType}</p>
                  <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-400">
                    <span className={`rounded-full px-2 py-0.5 ${client.isActive ? 'bg-emerald-500/15 text-emerald-300' : 'bg-slate-700 text-slate-300'}`}>
                      {client.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                    {client.ramalTransfHumano && <span>Ramal humano: {client.ramalTransfHumano}</span>}
                    {client.sipTrunkPrefix && <span>SIP: {client.sipTrunkPrefix}</span>}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => onEdit(client)}
                    className="rounded-lg bg-slate-800 p-2 hover:bg-slate-700 transition-colors text-cyan-300"
                    title="Editar"
                  >
                    <Edit2 className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => onDelete(client.id)}
                    className="rounded-lg bg-slate-800 p-2 hover:bg-rose-900/40 transition-colors text-rose-400"
                    title="Deletar"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </article>
          ))
        )}
      </div>
    </section>
  )
}

function ConversationCard({ session }) {
  const [isExpanded, setIsExpanded] = useState(false)

  const telemetry = session.telemetry ?? {}
  const loopTone = telemetry.loopRisk === 'high'
    ? 'bg-rose-500/15 text-rose-300 border-rose-500/30'
    : telemetry.loopRisk === 'medium'
      ? 'bg-amber-500/15 text-amber-300 border-amber-500/30'
      : 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30'

  const endedAtLabel = session.endedAt ? new Date(session.endedAt).toLocaleString() : null
  const finalActionLabel = session.finalAction ?? 'CHAMADA_ENCERRADA'
  const finalLayerLabel = session.finalResolutionLayer ?? 'N/D'
  const hasClosingMessage = Boolean(session.endedAt || session.finalAction)

  const sortedMessages = [...(session.messages ?? [])]
    .sort((left, right) => {
      const leftOrder = Number(left.interactionOrder ?? 0)
      const rightOrder = Number(right.interactionOrder ?? 0)
      if (leftOrder !== rightOrder) return leftOrder - rightOrder
      return new Date(left.at).getTime() - new Date(right.at).getTime()
    })

  const hasPersistedClosingMessage = sortedMessages.some((message) => (
    message.role === 'assistant'
    && typeof message.text === 'string'
    && message.text.toLowerCase().includes('atendimento encerrado')
  ))

  const renderedMessages = hasClosingMessage && !hasPersistedClosingMessage
    ? [...sortedMessages, {
      id: `${session.sessionId}-final-summary`,
      role: 'assistant',
      text: `Atendimento encerrado. Ação final: ${finalActionLabel}.`,
      at: session.endedAt ?? session.lastInteractionAt ?? session.startedAt,
      metrics: {
        resolutionLayer: session.finalResolutionLayer,
      },
      synthetic: true,
    }]
    : sortedMessages

  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-900/60 p-4">
      <header className="mb-3">
        <button
          type="button"
          onClick={() => setIsExpanded((prev) => !prev)}
          className="w-full rounded-xl border border-slate-800 bg-slate-950/50 p-3 text-left hover:border-slate-700"
        >
          <div className="flex items-center justify-between gap-2 text-xs text-slate-400">
            <div className="flex flex-wrap items-center gap-2">
              <span className="rounded-full bg-cyan-500/15 px-2 py-0.5 text-cyan-300">{session.tenantName}</span>
              <span>PID {session.tenantPid}</span>
              <span>Caller: {session.callerId ?? '--'}</span>
              <span>Interações: {session.interactionCount}</span>
              <span className="rounded-full border border-slate-700 px-2 py-0.5 text-slate-300">Extração final: {finalLayerLabel}</span>
              <span className={`rounded-full border px-2 py-0.5 ${loopTone}`}>Loop: {telemetry.loopRisk ?? 'low'}</span>
            </div>
            <div className="flex items-center gap-2">
              <span>{new Date(session.startedAt).toLocaleString()}</span>
              <span className="rounded-full bg-slate-800 px-2 py-0.5 text-slate-300">{session.finalAction ?? '--'}</span>
              {isExpanded ? <ChevronUp className="h-4 w-4 text-slate-400" /> : <ChevronDown className="h-4 w-4 text-slate-400" />}
            </div>
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-2 text-[11px] text-slate-500">
            <span>LLM avg: {prettyMs(telemetry.avgLlmProcessingTimeMs)}</span>
            <span>Interação avg: {prettyMs(telemetry.avgInteractionDurationMs)}</span>
            <span>Repetição assistente: {telemetry.repeatedAssistantPrompts ?? 0}</span>
            <span>Repetição por slot: {telemetry.repeatedAssistantSlotRequests ?? 0}</span>
            <span>Repetição visitante: {telemetry.repeatedVisitorUtterances ?? 0}</span>
            <span>Pedidos de documento: {telemetry.documentRequestCount ?? 0}</span>
            <span>Mensagens fracas: {telemetry.lowInfoVisitorMessages ?? 0}</span>
            <span>{endedAtLabel ? `Encerrado em: ${endedAtLabel}` : 'Atendimento em andamento'}</span>
          </div>
        </button>
      </header>

      {isExpanded && (
        <>
          <div className="space-y-2">
            {renderedMessages.length === 0 ? (
              <p className="text-xs text-slate-500">Sem mensagens transcritas para esta sessão.</p>
            ) : (
              renderedMessages.map((message) => {
                const isAssistant = message.role === 'assistant'
                const avatarSrc = isAssistant ? '/avatars/robot-portaria.svg' : '/avatars/visitor-unknown.svg'
                const avatarAlt = isAssistant ? 'Avatar robô da portaria' : 'Avatar visitante indefinido'
                const resolutionLayer = message?.metrics?.resolutionLayer
                const extractedDataJson = message?.metrics?.extractedDataJson

                return (
                  <div key={message.id} className={`flex items-end gap-2 ${isAssistant ? 'justify-start' : 'justify-end'}`}>
                    {isAssistant && (
                      <img src={avatarSrc} alt={avatarAlt} className="h-8 w-8 rounded-full border border-cyan-700/50 bg-slate-950" />
                    )}
                    <div
                      className={`max-w-[82%] rounded-2xl px-3 py-2 text-sm leading-relaxed ${
                        isAssistant
                          ? 'bg-slate-800 text-slate-100 rounded-bl-md'
                          : 'bg-cyan-600/20 text-cyan-100 border border-cyan-500/30 rounded-br-md'
                      }`}
                    >
                      <p className="whitespace-pre-wrap break-words">{message.text}</p>
                      <div className="mt-1 flex items-center justify-between gap-2 text-[11px] text-slate-400">
                        <span className="flex items-center gap-2">
                          <span>{isAssistant ? 'Assistente' : 'Visitante'}</span>
                          {resolutionLayer && (
                            <span className="rounded-full border border-cyan-700/40 bg-cyan-900/30 px-2 py-0.5 text-[10px] text-cyan-200">
                              {String(resolutionLayer).toUpperCase()}
                            </span>
                          )}
                          {message.synthetic && (
                            <span className="rounded-full border border-slate-600 bg-slate-800 px-2 py-0.5 text-[10px] text-slate-300">
                              Encerramento
                            </span>
                          )}
                          {extractedDataJson && (
                            <span className="rounded-full border border-emerald-700/40 bg-emerald-900/20 px-2 py-0.5 text-[10px] text-emerald-300">
                              Dados extraídos persistidos
                            </span>
                          )}
                        </span>
                        <span>{new Date(message.at).toLocaleTimeString()}</span>
                      </div>
                    </div>
                    {!isAssistant && (
                      <img src={avatarSrc} alt={avatarAlt} className="h-8 w-8 rounded-full border border-slate-600 bg-slate-950" />
                    )}
                  </div>
                )
              })
            )}
          </div>
                    <p className="text-xs text-slate-500">Datas/horas do filtro usam seu horário local e são convertidas para UTC na consulta.</p>

          <footer className="mt-3 flex flex-wrap items-center gap-3 text-[11px] text-slate-500">
            <span>Sessão: {session.sessionId}</span>
            <span>Tempo interação: {prettyMs(session.totalInteractionDurationMs)}</span>
            <span>Tempo IA: {prettyMs(session.totalLlmProcessingTimeMs)}</span>
          </footer>
        </>
      )}
    </article>
  )
}

function App() {
  const [activeTab, setActiveTab] = useState('health')
  const [showClientForm, setShowClientForm] = useState(false)
  const [editingClient, setEditingClient] = useState(null)
  const [formData, setFormData] = useState({
    pid: '',
    nomeIdentificador: '',
    tipoLocal: 'RESIDENCIAL',
    systemType: 'condominio',
    webhookUrl: '',
    apiToken: '',
    sipTrunkPrefix: '',
    ramalTransfHumano: '',
    usaBloco: false,
    usaTorre: false,
    recordingEnabled: false,
    isActive: true,
  })
  const [conversationFilters, setConversationFilters] = useState({
    tenantId: '',
    keyword: '',
    sessionId: '',
    loopRisk: '',
    fromUtc: '',
    toUtc: '',
    limit: 20,
    offset: 0,
  })

  const healthQuery = useHealthSnapshot()
  const recentEventsQuery = useRecentEvents()
  const conversationsQuery = useConversationThreads(conversationFilters)
  const { events: sseEvents, isConnected: sseConnected } = useLiveEventsSSE()
  const clientsQuery = useClientsList()

  const health = healthQuery.data ?? {}
  const dbHealth = health.database ?? {}
  const aiHealth = health.ai ?? {}
  const asteriskHealth = health.asterisk ?? {}
  const activeCalls = health.activeCalls ?? asteriskHealth.activeCalls ?? 0
  const latencyMs = aiHealth.latencyMs ?? null

  const mergedEvents = useMemo(() => {
    const all = [...sseEvents, ...(recentEventsQuery.data ?? [])]
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
  }, [recentEventsQuery.data, sseEvents])

  const visibleConversationSessions = conversationsQuery.data?.sessions ?? []

  const serverRiskLabel = conversationFilters.loopRisk
    ? `Filtro risco: ${conversationFilters.loopRisk.toUpperCase()}`
    : 'Filtro risco: TODOS'

  const handleNewClient = () => {
    setEditingClient(null)
    setFormData({
      pid: '',
      nomeIdentificador: '',
      tipoLocal: 'RESIDENCIAL',
      systemType: 'condominio',
      webhookUrl: '',
      apiToken: '',
      sipTrunkPrefix: '',
      ramalTransfHumano: '',
      usaBloco: false,
      usaTorre: false,
      recordingEnabled: false,
      isActive: true,
    })
    setShowClientForm(true)
  }

  const handleEditClient = (client) => {
    setEditingClient(client)
    setFormData({
      pid: String(client.pid),
      nomeIdentificador: client.nomeIdentificador,
      tipoLocal: client.tipoLocal,
      systemType: client.systemType,
      webhookUrl: client.webhookUrl,
      apiToken: client.apiToken,
      sipTrunkPrefix: client.sipTrunkPrefix,
      ramalTransfHumano: client.ramalTransfHumano,
      usaBloco: Boolean(client.usaBloco),
      usaTorre: Boolean(client.usaTorre),
      recordingEnabled: Boolean(client.recordingEnabled),
      isActive: Boolean(client.isActive),
    })
    setShowClientForm(true)
  }

  const handleDeleteClient = async (clientId) => {
    if (!confirm('Tem certeza que deseja deletar este cliente?')) return
    try {
      const response = await fetch(`${API_BASE}/api/tenants/${clientId}`, { method: 'DELETE' })
      if (!response.ok) throw new Error(`Erro ${response.status}`)
      clientsQuery.refetch()
      alert('Cliente deletado com sucesso!')
    } catch (error) {
      console.error('Erro ao deletar cliente:', error)
      alert('Erro ao deletar cliente')
    }
  }

  const handleSaveClient = async (e) => {
    e.preventDefault()
    try {
      const url = editingClient ? `${API_BASE}/api/tenants/${editingClient.id}` : `${API_BASE}/api/tenants`
      const method = editingClient ? 'PUT' : 'POST'
      const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          pid: Number(formData.pid),
          nomeIdentificador: formData.nomeIdentificador,
          tipoLocal: formData.tipoLocal,
          systemType: formData.systemType,
          webhookUrl: formData.webhookUrl || null,
          apiToken: formData.apiToken || null,
          sipTrunkPrefix: formData.sipTrunkPrefix || null,
          ramalTransfHumano: formData.ramalTransfHumano || null,
          usaBloco: formData.usaBloco,
          usaTorre: formData.usaTorre,
          recordingEnabled: formData.recordingEnabled,
          isActive: formData.isActive,
        }),
      })
      if (!response.ok) throw new Error(`Erro ${response.status}`)
      setShowClientForm(false)
      clientsQuery.refetch()
      alert(`Cliente ${editingClient ? 'atualizado' : 'criado'} com sucesso!`)
    } catch (error) {
      console.error('Erro ao salvar cliente:', error)
      alert('Erro ao salvar cliente')
    }
  }

  const handleFormChange = (e) => {
    const { name, value, type, checked } = e.target
    setFormData((prev) => ({ ...prev, [name]: type === 'checkbox' ? checked : value }))
  }

  const handleConversationFilterChange = (e) => {
    const { name, value } = e.target
    setConversationFilters((prev) => ({ ...prev, [name]: value, offset: 0 }))
  }

  const handleConversationSearch = (e) => {
    e.preventDefault()
    conversationsQuery.refetch()
  }

  const handleConversationPage = (direction) => {
    const limit = Number(conversationFilters.limit ?? 20)
    setConversationFilters((prev) => {
      const currentOffset = Number(prev.offset ?? 0)
      const nextOffset = direction === 'next'
        ? currentOffset + limit
        : Math.max(0, currentOffset - limit)

      return { ...prev, offset: nextOffset }
    })
  }

  const handleExportConversationsJson = () => {
    const sessions = visibleConversationSessions
    const payload = {
      exportedAt: new Date().toISOString(),
      filters: {
        ...conversationFilters,
      },
      totalCount: sessions.length,
      sessions,
    }

    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `conversas-${new Date().toISOString().replace(/[:.]/g, '-')}.json`
    document.body.appendChild(anchor)
    anchor.click()
    document.body.removeChild(anchor)
    URL.revokeObjectURL(url)
  }

  const handleExportConversationsCsv = () => {
    const sessions = visibleConversationSessions
    const rows = ['sessionId,tenantId,tenantPid,tenantName,callerId,startedAt,endedAt,finalAction,role,text,at']

    for (const session of sessions) {
      for (const message of session.messages ?? []) {
        const values = [
          session.sessionId,
          session.tenantId,
          session.tenantPid,
          session.tenantName,
          session.callerId ?? '',
          session.startedAt,
          session.endedAt ?? '',
          session.finalAction ?? '',
          message.role ?? '',
          (message.text ?? '').replace(/"/g, '""'),
          message.at,
        ]

        const csvLine = values.map((value) => `"${String(value ?? '').replace(/"/g, '""')}"`).join(',')
        rows.push(csvLine)
      }
    }

    const blob = new Blob([rows.join('\n')], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `conversas-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`
    document.body.appendChild(anchor)
    anchor.click()
    document.body.removeChild(anchor)
    URL.revokeObjectURL(url)
  }

  const topSummary = useMemo(
    () => [
      { label: 'Overall', value: formatStatus(health.overall), icon: ShieldCheck },
      { label: 'Chamadas Ativas', value: String(activeCalls), icon: PhoneCall },
      { label: 'Latencia IA', value: prettyMs(latencyMs), icon: Activity },
    ],
    [activeCalls, health.overall, latencyMs],
  )

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto max-w-6xl px-4 pb-28 pt-6 sm:px-6 lg:px-8">
        <header className="mb-6 rounded-2xl border border-cyan-400/20 bg-gradient-to-r from-slate-900 via-slate-900 to-cyan-950/40 p-5 shadow-lg shadow-cyan-900/20">
          <p className="text-xs uppercase tracking-[0.22em] text-cyan-300">Portaria Virtual SaaS</p>
          <h1 className="mt-2 text-2xl font-semibold sm:text-3xl">Console Administrativo Multi-Client</h1>
          <p className="mt-2 text-sm text-slate-400">Monitoramento de saude, configuracoes de dominios e eventos em tempo real.</p>
        </header>

        <section className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-3">
          {topSummary.map((item) => (
            <article key={item.label} className="glass-card flex items-center justify-between">
              <div>
                <p className="text-xs uppercase tracking-wide text-slate-400">{item.label}</p>
                <p className="mt-1 text-lg font-semibold text-slate-100">{item.value}</p>
              </div>
              <item.icon className="h-5 w-5 text-cyan-300" />
            </article>
          ))}
        </section>

        {activeTab === 'health' && (
          <main className="space-y-5">
            <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
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
            </section>

            <section className="glass-card">
              <p className="text-sm text-slate-400">Atualizacao automatica</p>
              <p className="mt-1 text-sm text-slate-200">O health check e consultado a cada 30 segundos em <span className="font-mono text-cyan-300">/api/health</span>.</p>
              <p className="mt-2 text-xs text-slate-400">CPU e memoria nao sao exibidos porque o backend ainda nao fornece telemetria real dessas metricas.</p>
              {healthQuery.isFetching && <p className="mt-2 text-xs text-cyan-300">Atualizando status...</p>}
              {healthQuery.isError && <p className="mt-2 text-xs text-rose-300">Nao foi possivel consultar o backend agora.</p>}
            </section>
          </main>
        )}

        {activeTab === 'clients' && (
          <main className="space-y-5">
            <ClientsList
              clients={clientsQuery.data ?? []}
              onEdit={handleEditClient}
              onDelete={handleDeleteClient}
              onNew={handleNewClient}
            />

            {showClientForm && (
              <section className="glass-card">
                <div className="mb-4 flex items-center justify-between">
                  <h2 className="flex items-center gap-2 text-lg font-semibold">
                    <Building2 className="h-5 w-5 text-cyan-300" />
                    {editingClient ? 'Editar Cliente' : 'Novo Cliente'}
                  </h2>
                  <button
                    onClick={() => setShowClientForm(false)}
                    className="rounded-lg bg-slate-800 p-1.5 hover:bg-slate-700 transition-colors"
                  >
                    <X className="h-5 w-5 text-slate-400" />
                  </button>
                </div>
                <form className="grid grid-cols-1 gap-3 sm:grid-cols-2" onSubmit={handleSaveClient}>
                  <input
                    className="field"
                    placeholder="PID do tenant"
                    name="pid"
                    value={formData.pid}
                    onChange={handleFormChange}
                    required
                    type="number"
                    min="1"
                  />
                  <input
                    className="field"
                    placeholder="Nome do condominio"
                    name="nomeIdentificador"
                    value={formData.nomeIdentificador}
                    onChange={handleFormChange}
                    required
                  />
                  <select
                    className="field"
                    name="tipoLocal"
                    value={formData.tipoLocal}
                    onChange={handleFormChange}
                    required
                  >
                    <option value="RESIDENCIAL">Residencial</option>
                    <option value="COMERCIAL">Comercial</option>
                    <option value="HOSPITALAR">Hospitalar</option>
                    <option value="INDUSTRIAL">Industrial</option>
                  </select>
                  <input
                    className="field"
                    placeholder="System Type"
                    name="systemType"
                    value={formData.systemType}
                    onChange={handleFormChange}
                    required
                  />
                  <input
                    className="field"
                    placeholder="Webhook URL"
                    name="webhookUrl"
                    value={formData.webhookUrl}
                    onChange={handleFormChange}
                  />
                  <textarea
                    className="field sm:col-span-2"
                    placeholder="API token do tenant"
                    name="apiToken"
                    value={formData.apiToken}
                    onChange={handleFormChange}
                    rows={3}
                  />
                  <div className="sm:col-span-2 grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <input
                      className="field"
                      placeholder="Sip Trunk Prefix"
                      name="sipTrunkPrefix"
                      value={formData.sipTrunkPrefix}
                      onChange={handleFormChange}
                    />
                    <input
                      className="field"
                      placeholder="Ramal de transferencia humana"
                      name="ramalTransfHumano"
                      value={formData.ramalTransfHumano}
                      onChange={handleFormChange}
                    />
                  </div>
                  <label className="flex items-center gap-2 text-sm text-slate-300">
                    <input type="checkbox" name="usaBloco" checked={formData.usaBloco} onChange={handleFormChange} />
                    Usa bloco
                  </label>
                  <label className="flex items-center gap-2 text-sm text-slate-300">
                    <input type="checkbox" name="usaTorre" checked={formData.usaTorre} onChange={handleFormChange} />
                    Usa torre
                  </label>
                  <label className="flex items-center gap-2 text-sm text-slate-300">
                    <input type="checkbox" name="recordingEnabled" checked={formData.recordingEnabled} onChange={handleFormChange} />
                    Grava chamadas
                  </label>
                  <label className="flex items-center gap-2 text-sm text-slate-300">
                    <input type="checkbox" name="isActive" checked={formData.isActive} onChange={handleFormChange} />
                    Tenant ativo
                  </label>
                  <div className="sm:col-span-2 flex items-center gap-3">
                    <button className="btn-primary flex-1" type="submit">
                      {editingClient ? 'Atualizar cliente' : 'Criar novo cliente'}
                    </button>
                    <button
                      className="flex-1 rounded-lg border border-slate-700 bg-slate-800/50 px-4 py-2 font-medium text-slate-300 hover:border-slate-600 hover:bg-slate-700/50 transition-colors"
                      type="button"
                      onClick={() => setShowClientForm(false)}
                    >
                      Cancelar
                    </button>
                  </div>
                </form>
              </section>
            )}

            {clientsQuery.isFetching && <p className="text-xs text-cyan-300">Atualizando lista de clientes...</p>}
            {clientsQuery.isError && <p className="text-xs text-rose-300">Erro ao carregar tenants reais do backend.</p>}
          </main>
        )}

        {activeTab === 'logs' && (
          <main className="space-y-5">
            <section className="glass-card">
              <div className="mb-4 flex items-center justify-between">
                <h2 className="text-lg font-semibold">Eventos em Tempo Real (SSE)</h2>
                <div className="flex items-center gap-2">
                  <span className={`h-2.5 w-2.5 rounded-full ${sseConnected ? 'bg-emerald-500 animate-pulse' : 'bg-rose-500'}`} />
                  <span className="text-xs text-slate-400">{sseConnected ? 'Conectado' : 'Desconectado'}</span>
                </div>
              </div>
              <div className="space-y-2">
                {mergedEvents.length === 0 ? (
                  <p className="py-6 text-center text-sm text-slate-400">
                    Aguardando eventos em tempo real...
                    <br />
                    <span className="text-xs text-slate-500">(Chamadas aparecerão aqui quando iniciadas)</span>
                  </p>
                ) : (
                  mergedEvents.map((event, index) => (
                    <article key={`${event.id || event.Id || event.at || event.At}-${index}`} className="rounded-xl border border-slate-800 bg-slate-900/60 p-3">
                      <div className="flex items-center justify-between gap-3 text-xs text-slate-400">
                        <span className="inline-flex items-center gap-1.5">
                          <span className={`h-1.5 w-1.5 rounded-full ${(event.level ?? event.Level) === 'error' ? 'bg-rose-500' : (event.level ?? event.Level) === 'warn' ? 'bg-amber-500' : 'bg-emerald-500'}`} />
                          <span className="uppercase tracking-wide">{event.level ?? event.Level ?? 'info'}</span>
                        </span>
                        <span>{(event.category ?? event.Category) && `[${event.category ?? event.Category}]`}</span>
                        <span>{new Date(event.at ?? event.At).toLocaleTimeString()}</span>
                      </div>
                      <p className="mt-2 text-sm text-slate-200">{event.message ?? event.Message}</p>
                    </article>
                  ))
                )}
              </div>

              {!sseConnected && (
                <div className="mt-4 rounded-lg border border-amber-500/30 bg-amber-950/20 p-3 text-xs text-amber-300">
                  <p>Aguardando reconexão ao servidor de eventos...</p>
                </div>
              )}
            </section>

            <section className="glass-card">
              <h2 className="mb-3 text-lg font-semibold">Conversas (Visualização Humana)</h2>

              <form className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3" onSubmit={handleConversationSearch}>
                <select className="field" name="tenantId" value={conversationFilters.tenantId} onChange={handleConversationFilterChange}>
                  <option value="">Todos os tenants</option>
                  {(clientsQuery.data ?? []).map((tenant) => (
                    <option key={tenant.id} value={tenant.id}>
                      {tenant.nomeIdentificador} (PID {tenant.pid})
                    </option>
                  ))}
                </select>

                <input
                  className="field"
                  name="keyword"
                  value={conversationFilters.keyword}
                  onChange={handleConversationFilterChange}
                  placeholder="Palavra-chave (ex: apartamento, documento)"
                />

                <input
                  className="field"
                  name="sessionId"
                  value={conversationFilters.sessionId}
                  onChange={handleConversationFilterChange}
                  placeholder="SessionId específica"
                />

                <input
                  className="field"
                  type="datetime-local"
                  name="fromUtc"
                  value={conversationFilters.fromUtc}
                  onChange={handleConversationFilterChange}
                />

                <input
                  className="field"
                  type="datetime-local"
                  name="toUtc"
                  value={conversationFilters.toUtc}
                  onChange={handleConversationFilterChange}
                />

                <div className="flex items-center gap-2">
                  <select className="field" name="limit" value={conversationFilters.limit} onChange={handleConversationFilterChange}>
                    <option value="10">10 sessões</option>
                    <option value="20">20 sessões</option>
                    <option value="50">50 sessões</option>
                    <option value="100">100 sessões</option>
                  </select>
                  <button className="btn-primary" type="submit">Buscar</button>
                </div>
              </form>

              <div className="mt-4 space-y-3">
                {conversationsQuery.isFetching && <p className="text-xs text-cyan-300">Consultando conversas...</p>}
                {conversationsQuery.isError && <p className="text-xs text-rose-300">Erro ao consultar conversas reais.</p>}

                <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-800 bg-slate-900/40 px-3 py-2 text-xs text-slate-400">
                  <div className="flex flex-wrap items-center gap-2">
                    <span>Total filtrado (backend): {conversationsQuery.data?.totalCount ?? 0}</span>
                    <span className={`rounded-full border px-2 py-0.5 ${conversationFilters.loopRisk === 'high' ? 'border-rose-500/40 bg-rose-500/15 text-rose-300' : 'border-slate-700 bg-slate-800 text-slate-300'}`}>{serverRiskLabel}</span>
                    <span>Offset (backend): {Number(conversationFilters.offset ?? 0)}</span>
                    <span>Limite: {Number(conversationFilters.limit ?? 20)}</span>
                    <label className="inline-flex items-center gap-2 rounded-lg border border-slate-700 bg-slate-900 px-2 py-1 text-slate-200">
                      <input
                        type="checkbox"
                        checked={conversationFilters.loopRisk === 'high'}
                        onChange={(e) => setConversationFilters((prev) => ({ ...prev, loopRisk: e.target.checked ? 'high' : '', offset: 0 }))}
                      />
                      Somente risco alto
                    </label>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      className="rounded-lg border border-slate-700 px-2 py-1 text-slate-300 hover:bg-slate-800 disabled:opacity-40"
                      disabled={!conversationsQuery.data?.pagination?.hasPrevious || conversationsQuery.isFetching}
                      onClick={() => handleConversationPage('previous')}
                    >
                      Anterior
                    </button>
                    <button
                      type="button"
                      className="rounded-lg border border-slate-700 px-2 py-1 text-slate-300 hover:bg-slate-800 disabled:opacity-40"
                      disabled={!conversationsQuery.data?.pagination?.hasNext || conversationsQuery.isFetching}
                      onClick={() => handleConversationPage('next')}
                    >
                      Próxima
                    </button>
                    <button
                      type="button"
                      className="rounded-lg border border-cyan-700 px-2 py-1 text-cyan-300 hover:bg-cyan-950/40 disabled:opacity-40"
                      disabled={visibleConversationSessions.length === 0}
                      onClick={handleExportConversationsJson}
                    >
                      Exportar JSON
                    </button>
                    <button
                      type="button"
                      className="rounded-lg border border-cyan-700 px-2 py-1 text-cyan-300 hover:bg-cyan-950/40 disabled:opacity-40"
                      disabled={visibleConversationSessions.length === 0}
                      onClick={handleExportConversationsCsv}
                    >
                      Exportar CSV
                    </button>
                  </div>
                </div>

                <p className="text-xs text-slate-500">
                  Contagem e paginação são calculadas no servidor com os filtros atuais.
                </p>

                {!conversationsQuery.isFetching && visibleConversationSessions.length === 0 && (
                  <p className="py-6 text-center text-sm text-slate-400">
                    {conversationFilters.loopRisk === 'high'
                      ? 'Nenhuma conversa com risco alto para os filtros atuais.'
                      : 'Nenhuma conversa encontrada com os filtros atuais.'}
                  </p>
                )}

                {visibleConversationSessions.map((session) => (
                  <ConversationCard key={session.sessionId} session={session} />
                ))}
              </div>
            </section>
          </main>
        )}
      </div>

      <nav className="fixed inset-x-0 bottom-0 z-50 border-t border-slate-800 bg-slate-950/95 backdrop-blur sm:left-1/2 sm:max-w-md sm:-translate-x-1/2 sm:rounded-t-2xl sm:border-x">
        <ul className="grid grid-cols-3">
          {tabs.map((tab) => {
            const active = tab.id === activeTab
            return (
              <li key={tab.id}>
                <button
                  className={`flex w-full flex-col items-center gap-1 py-3 text-xs ${active ? 'text-cyan-300' : 'text-slate-400'}`}
                  onClick={() => setActiveTab(tab.id)}
                >
                  <tab.icon className={`h-5 w-5 ${active ? 'text-cyan-300' : 'text-slate-500'}`} />
                  {tab.label}
                </button>
              </li>
            )
          })}
        </ul>
      </nav>
    </div>
  )
}

export default App
