import { Server } from 'lucide-react'

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

function normalizeProviders(providers) {
  return Array.isArray(providers) ? providers : []
}

function providerShortcutUrl(provider) {
  if (!provider) return null
  if (String(provider.serviceType).toUpperCase() === 'GOOGLEAI') return 'https://aistudio.google.com/app/apikey'
  return provider.endpoint || null
}

function providerShortcutLabel(provider) {
  if (!provider) return 'Abrir endpoint'
  if (String(provider.serviceType).toUpperCase() === 'GOOGLEAI') return 'Abrir Gemini / AI Studio'
  return 'Abrir endpoint'
}

function providerHttpLabel(provider) {
  if (provider?.httpStatusCode == null) return null
  return `HTTP ${provider.httpStatusCode}`
}

function prettyMs(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'
  return `${Math.round(Number(value))} ms`
}

export function LlmProvidersPanel({ llm }) {
  const providers = normalizeProviders(llm?.providers)

  if (providers.length === 0) {
    return (
      <section className="glass-card">
        <div className="flex items-center justify-between gap-3">
          <div>
            <p className="text-xs uppercase tracking-[0.18em] text-slate-500">LLM Providers</p>
            <h3 className="mt-1 text-lg font-semibold text-slate-100">Nenhum provedor exposto</h3>
          </div>
          <Server className="h-5 w-5 text-cyan-300" />
        </div>
        <p className="mt-3 text-sm text-slate-400">O health atual não trouxe detalhes dos provedores LLM habilitados.</p>
      </section>
    )
  }

  return (
    <section className="glass-card">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-xs uppercase tracking-[0.18em] text-slate-500">LLM Providers</p>
          <h3 className="mt-1 text-lg font-semibold text-slate-100">Saúde detalhada dos provedores de IA</h3>
          <p className="mt-1 text-sm text-slate-400">Veja qual IA está com problema e abra rapidamente o endpoint ou o atalho do Gemini.</p>
        </div>
        <span className={`rounded-full border px-3 py-1 text-xs font-medium ${statusTone(llm?.status) === 'ok' ? 'border-emerald-400/20 bg-emerald-500/15 text-emerald-200' : 'border-rose-400/20 bg-rose-500/15 text-rose-200'}`}>
          {formatStatus(llm?.status)}
        </span>
      </div>

      <div className="mt-4 grid grid-cols-1 gap-3 xl:grid-cols-2">
        {providers.map((provider) => {
          const tone = statusTone(provider.status)
          const toneBorder = tone === 'ok'
            ? 'border-emerald-500/20'
            : tone === 'warn'
              ? 'border-amber-500/20'
              : 'border-rose-500/20'
          const toneBadge = tone === 'ok'
            ? 'bg-emerald-500/15 text-emerald-200'
            : tone === 'warn'
              ? 'bg-amber-500/15 text-amber-200'
              : 'bg-rose-500/15 text-rose-200'
          const shortcut = providerShortcutUrl(provider)

          return (
            <article key={`${provider.name}-${provider.serviceType}`} className={`rounded-2xl border ${toneBorder} bg-slate-950/45 p-4`}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="flex items-center gap-2">
                    <p className="text-base font-semibold text-slate-100">{provider.name}</p>
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${toneBadge}`}>{formatStatus(provider.status)}</span>
                  </div>
                  <p className="mt-1 text-xs uppercase tracking-wide text-slate-500">{provider.serviceType} • {provider.model ?? 'modelo não informado'}</p>
                </div>
                <div className="text-right text-xs text-slate-500">
                  {providerHttpLabel(provider) && <p>{providerHttpLabel(provider)}</p>}
                  {provider.elapsedMs != null && <p>{prettyMs(provider.elapsedMs)}</p>}
                </div>
              </div>

              <p className="mt-3 text-sm text-slate-300">{provider.message ?? 'Sem mensagem retornada.'}</p>

              <div className="mt-4 flex flex-wrap gap-2 text-xs">
                {provider.endpoint && (
                  <a
                    href={provider.endpoint}
                    target="_blank"
                    rel="noreferrer"
                    className="rounded-lg border border-cyan-400/20 bg-cyan-500/10 px-3 py-1.5 font-medium text-cyan-200 transition hover:bg-cyan-500/20"
                  >
                    Abrir endpoint
                  </a>
                )}
                {shortcut && (
                  <a
                    href={shortcut}
                    target="_blank"
                    rel="noreferrer"
                    className="rounded-lg border border-violet-400/20 bg-violet-500/10 px-3 py-1.5 font-medium text-violet-200 transition hover:bg-violet-500/20"
                  >
                    {providerShortcutLabel(provider)}
                  </a>
                )}
              </div>
            </article>
          )
        })}
      </div>
    </section>
  )
}