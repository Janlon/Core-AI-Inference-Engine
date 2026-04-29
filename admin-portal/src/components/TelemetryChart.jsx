import { prettyPercent } from '../lib/portalFormatters'

export function TelemetryChart({ title, valueLabel, subtitle, series, strokeClass, fillClass }) {
  const width = 640
  const height = 220
  const paddingX = 18
  const paddingTop = 20
  const paddingBottom = 28
  const plotHeight = height - paddingTop - paddingBottom
  const plotWidth = width - paddingX * 2
  const validPoints = series.filter((item) => item.value != null && !Number.isNaN(Number(item.value)))

  const points = validPoints.map((item, index) => {
    const safeValue = Math.max(0, Math.min(100, Number(item.value)))
    const x = validPoints.length <= 1
      ? width / 2
      : paddingX + (index / (validPoints.length - 1)) * plotWidth
    const y = paddingTop + ((100 - safeValue) / 100) * plotHeight

    return {
      ...item,
      x,
      y,
      safeValue,
    }
  })

  const linePath = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`)
    .join(' ')

  const areaPath = points.length > 0
    ? `${linePath} L ${points[points.length - 1].x.toFixed(1)} ${(height - paddingBottom).toFixed(1)} L ${points[0].x.toFixed(1)} ${(height - paddingBottom).toFixed(1)} Z`
    : ''

  const latestPoint = points[points.length - 1] ?? null
  const minValue = points.length > 0 ? Math.min(...points.map((point) => point.safeValue)) : null
  const maxValue = points.length > 0 ? Math.max(...points.map((point) => point.safeValue)) : null
  const xLabels = [points[0], points[Math.floor((points.length - 1) / 2)], points[points.length - 1]].filter(Boolean)

  return (
    <article className="glass-card overflow-hidden">
      <div className="mb-4 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.18em] text-slate-500">Telemetria</p>
          <h3 className="mt-1 text-lg font-semibold text-slate-100">{title}</h3>
          <p className="mt-1 text-xs text-slate-400">{subtitle}</p>
        </div>
        <div className="text-right">
          <p className="text-2xl font-semibold text-slate-100">{valueLabel}</p>
          <p className="mt-1 text-xs text-slate-500">Janela local com {series.length} amostras</p>
        </div>
      </div>

      {points.length === 0 ? (
        <div className="flex h-56 items-center justify-center rounded-2xl border border-dashed border-slate-800 bg-slate-950/40 text-sm text-slate-500">
          Aguardando amostras suficientes para desenhar o grafico.
        </div>
      ) : (
        <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-3">
          <svg viewBox={`0 0 ${width} ${height}`} className="h-56 w-full" preserveAspectRatio="none" role="img" aria-label={title}>
            {[0, 25, 50, 75, 100].map((tick) => {
              const y = paddingTop + ((100 - tick) / 100) * plotHeight
              return (
                <g key={tick}>
                  <line x1={paddingX} y1={y} x2={width - paddingX} y2={y} className="stroke-slate-800" strokeWidth="1" strokeDasharray="3 6" />
                  <text x={paddingX} y={y - 4} className="fill-slate-600 text-[10px]">{tick}%</text>
                </g>
              )
            })}

            {areaPath && <path d={areaPath} className={fillClass} />}
            {linePath && <path d={linePath} className={strokeClass} fill="none" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />}

            {points.map((point) => (
              <circle key={point.id} cx={point.x} cy={point.y} r="3.5" className={strokeClass} />
            ))}

            {xLabels.map((point) => (
              <text key={`${point.id}-label`} x={point.x} y={height - 8} textAnchor="middle" className="fill-slate-500 text-[10px]">
                {new Date(point.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </text>
            ))}
          </svg>

          <div className="mt-4 grid grid-cols-3 gap-3 text-xs text-slate-400">
            <div className="rounded-xl border border-slate-800 bg-slate-900/50 px-3 py-2">
              <p className="text-slate-500">Atual</p>
              <p className="mt-1 font-semibold text-slate-100">{latestPoint ? prettyPercent(latestPoint.safeValue) : '--'}</p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900/50 px-3 py-2">
              <p className="text-slate-500">Min</p>
              <p className="mt-1 font-semibold text-slate-100">{minValue == null ? '--' : prettyPercent(minValue)}</p>
            </div>
            <div className="rounded-xl border border-slate-800 bg-slate-900/50 px-3 py-2">
              <p className="text-slate-500">Max</p>
              <p className="mt-1 font-semibold text-slate-100">{maxValue == null ? '--' : prettyPercent(maxValue)}</p>
            </div>
          </div>
        </div>
      )}
    </article>
  )
}