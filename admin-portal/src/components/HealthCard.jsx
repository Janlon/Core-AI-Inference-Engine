import { formatStatus, statusTone } from '../lib/portalFormatters'

export function HealthCard({ title, status, detail, icon }) {
  const IconComponent = icon
  const tone = statusTone(status)
  const toneClass = tone === 'ok' ? 'bg-emerald-500' : tone === 'warn' ? 'bg-amber-500' : 'bg-rose-500'

  return (
    <article className="glass-card">
      <div className="mb-4 flex items-start justify-between">
        <div>
          <p className="text-sm text-slate-400">{title}</p>
          <p className="mt-1 text-lg font-semibold capitalize text-slate-100">{formatStatus(status)}</p>
        </div>
        <IconComponent className="h-5 w-5 text-cyan-300" />
      </div>
      <div className="mt-3 flex items-center gap-2 text-sm text-slate-400">
        <span className={`h-2.5 w-2.5 rounded-full ${toneClass}`} />
        <span>{detail}</span>
      </div>
    </article>
  )
}