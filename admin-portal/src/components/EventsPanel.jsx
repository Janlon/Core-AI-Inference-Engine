function EventItem({ eventKey, event }) {
  const level = event.level ?? event.Level ?? 'info'
  const category = event.category ?? event.Category
  const at = event.at ?? event.At
  const message = event.message ?? event.Message
  const tone = level === 'error' ? 'bg-rose-500' : level === 'warn' ? 'bg-amber-500' : 'bg-emerald-500'

  return (
    <article key={eventKey} className="rounded-xl border border-slate-800 bg-slate-900/60 p-3">
      <div className="flex items-center justify-between gap-3 text-xs text-slate-400">
        <span className="inline-flex items-center gap-1.5">
          <span className={`h-1.5 w-1.5 rounded-full ${tone}`} />
          <span className="uppercase tracking-wide">{level}</span>
        </span>
        <span>{category && `[${category}]`}</span>
        <span>{new Date(at).toLocaleTimeString()}</span>
      </div>
      <p className="mt-2 text-sm text-slate-200">{message}</p>
    </article>
  )
}

export function LiveEventsPanel({ sseConnected, realtimeEvents }) {
  return (
    <section className="glass-card">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold">Eventos em Tempo Real (SSE)</h2>
        <div className="flex items-center gap-2">
          <span className={`h-2.5 w-2.5 rounded-full ${sseConnected ? 'bg-emerald-500 animate-pulse' : 'bg-rose-500'}`} />
          <span className="text-xs text-slate-400">{sseConnected ? 'Conectado' : 'Desconectado'}</span>
        </div>
      </div>
      <div className="space-y-2">
        {realtimeEvents.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-400">
            Aguardando eventos em tempo real...
            <br />
            <span className="text-xs text-slate-500">(Esta lista mostra apenas o stream SSE atual, sem histórico antigo)</span>
          </p>
        ) : (
          realtimeEvents.map((event, index) => (
            <EventItem
              key={`${event.id || event.Id || event.at || event.At}-${index}`}
              eventKey={`${event.id || event.Id || event.at || event.At}-${index}`}
              event={event}
            />
          ))
        )}
      </div>

      {!sseConnected && (
        <div className="mt-4 rounded-lg border border-amber-500/30 bg-amber-950/20 p-3 text-xs text-amber-300">
          <p>Aguardando reconexão ao servidor de eventos...</p>
        </div>
      )}
    </section>
  )
}

export function RecentEventsPanel({ recentEvents }) {
  return (
    <section className="glass-card">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold">Historico Recente</h2>
        <span className="text-xs text-slate-400">Eventos já acumulados na memória do backend</span>
      </div>
      <div className="space-y-2">
        {recentEvents.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-400">Nenhum evento recente disponível.</p>
        ) : (
          recentEvents.map((event, index) => (
            <EventItem
              key={`${event.id || event.Id || event.at || event.At}-recent-${index}`}
              eventKey={`${event.id || event.Id || event.at || event.At}-recent-${index}`}
              event={event}
            />
          ))
        )}
      </div>
    </section>
  )
}