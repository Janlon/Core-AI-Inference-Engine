import { ConversationCard } from './ConversationCard'

export function ConversationsPanel({
  clients,
  conversationFilters,
  conversationsQuery,
  serverRiskLabel,
  visibleConversationSessions,
  onFilterChange,
  onSearch,
  onToggleHighRisk,
  onPreviousPage,
  onNextPage,
  onExportJson,
  onExportCsv,
}) {
  return (
    <section className="glass-card">
      <h2 className="mb-3 text-lg font-semibold">Conversas (Visualização Humana)</h2>

      <form className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3" onSubmit={onSearch}>
        <select className="field" name="tenantId" value={conversationFilters.tenantId} onChange={onFilterChange}>
          <option value="">Todos os tenants</option>
          {clients.map((tenant) => (
            <option key={tenant.id} value={tenant.id}>
              {tenant.nomeIdentificador} (PID {tenant.pid})
            </option>
          ))}
        </select>

        <input
          className="field"
          name="keyword"
          value={conversationFilters.keyword}
          onChange={onFilterChange}
          placeholder="Palavra-chave (ex: apartamento, documento)"
        />

        <input
          className="field"
          name="sessionId"
          value={conversationFilters.sessionId}
          onChange={onFilterChange}
          placeholder="SessionId específica"
        />

        <input
          className="field"
          type="datetime-local"
          name="fromUtc"
          value={conversationFilters.fromUtc}
          onChange={onFilterChange}
        />

        <input
          className="field"
          type="datetime-local"
          name="toUtc"
          value={conversationFilters.toUtc}
          onChange={onFilterChange}
        />

        <div className="flex items-center gap-2">
          <select className="field" name="limit" value={conversationFilters.limit} onChange={onFilterChange}>
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
                onChange={onToggleHighRisk}
              />
              Somente risco alto
            </label>
          </div>
          <div className="flex items-center gap-2">
            <button
              type="button"
              className="rounded-lg border border-slate-700 px-2 py-1 text-slate-300 hover:bg-slate-800 disabled:opacity-40"
              disabled={!conversationsQuery.data?.pagination?.hasPrevious || conversationsQuery.isFetching}
              onClick={onPreviousPage}
            >
              Anterior
            </button>
            <button
              type="button"
              className="rounded-lg border border-slate-700 px-2 py-1 text-slate-300 hover:bg-slate-800 disabled:opacity-40"
              disabled={!conversationsQuery.data?.pagination?.hasNext || conversationsQuery.isFetching}
              onClick={onNextPage}
            >
              Próxima
            </button>
            <button
              type="button"
              className="rounded-lg border border-cyan-700 px-2 py-1 text-cyan-300 hover:bg-cyan-950/40 disabled:opacity-40"
              disabled={visibleConversationSessions.length === 0}
              onClick={onExportJson}
            >
              Exportar JSON
            </button>
            <button
              type="button"
              className="rounded-lg border border-cyan-700 px-2 py-1 text-cyan-300 hover:bg-cyan-950/40 disabled:opacity-40"
              disabled={visibleConversationSessions.length === 0}
              onClick={onExportCsv}
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
  )
}