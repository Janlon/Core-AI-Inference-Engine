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

export function buildRobotMood({
  healthQueryError,
  overallStatus,
  aiStatus,
  dbStatus,
  speechStatus,
  asteriskStatus,
  sseConnected,
}) {
  if (healthQueryError) {
    return {
      mood: 'sleeping',
      title: 'Robô dormindo',
      subtitle: 'Sem comunicação com o backend agora.',
      badgeClass: 'bg-indigo-500/15 text-indigo-200 border-indigo-400/20',
    }
  }

  const tones = [overallStatus, aiStatus, dbStatus, speechStatus, asteriskStatus].map(statusTone)

  if (tones.includes('down')) {
    return {
      mood: 'sad',
      title: 'Robô preocupado',
      subtitle: 'Existe pelo menos um serviço crítico com falha.',
      badgeClass: 'bg-rose-500/15 text-rose-200 border-rose-400/20',
    }
  }

  if (!sseConnected || tones.includes('warn')) {
    return {
      mood: 'alert',
      title: 'Robô em alerta',
      subtitle: !sseConnected
        ? 'O stream em tempo real caiu, mas o backend ainda responde.'
        : 'Há sinais de degradação ou aquecimento em andamento.',
      badgeClass: 'bg-amber-500/15 text-amber-200 border-amber-400/20',
    }
  }

  return {
    mood: 'happy',
    title: 'Robô feliz',
    subtitle: 'Os serviços principais parecem saudáveis e se comunicando.',
    badgeClass: 'bg-emerald-500/15 text-emerald-200 border-emerald-400/20',
  }
}

function RobotStatusAvatar({ mood }) {
  const palette = {
    happy: {
      shell: '#22d3ee',
      face: '#0f172a',
      eye: '#67e8f9',
      blush: '#34d399',
      accent: '#10b981',
    },
    alert: {
      shell: '#f59e0b',
      face: '#111827',
      eye: '#fde68a',
      blush: '#fb923c',
      accent: '#f97316',
    },
    sad: {
      shell: '#fb7185',
      face: '#111827',
      eye: '#fecdd3',
      blush: '#f43f5e',
      accent: '#ef4444',
    },
    sleeping: {
      shell: '#818cf8',
      face: '#111827',
      eye: '#c7d2fe',
      blush: '#a78bfa',
      accent: '#8b5cf6',
    },
  }[mood] ?? {
    shell: '#22d3ee',
    face: '#0f172a',
    eye: '#67e8f9',
    blush: '#34d399',
    accent: '#10b981',
  }

  const eyeMarkup = {
    happy: (
      <>
        <path d="M54 82 q10 -12 20 0" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
        <path d="M106 82 q10 -12 20 0" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
      </>
    ),
    alert: (
      <>
        <ellipse cx="64" cy="82" rx="10" ry="12" fill={palette.eye} />
        <ellipse cx="116" cy="82" rx="10" ry="12" fill={palette.eye} />
        <circle cx="64" cy="82" r="3.5" fill={palette.face} />
        <circle cx="116" cy="82" r="3.5" fill={palette.face} />
      </>
    ),
    sad: (
      <>
        <path d="M54 76 q10 12 20 0" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
        <path d="M106 76 q10 12 20 0" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
      </>
    ),
    sleeping: (
      <>
        <path d="M53 82 h22" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
        <path d="M105 82 h22" stroke={palette.eye} strokeWidth="6" strokeLinecap="round" fill="none" />
      </>
    ),
  }[mood]

  const mouthMarkup = {
    happy: <path d="M61 117 q29 23 58 0" stroke={palette.accent} strokeWidth="7" strokeLinecap="round" fill="none" />,
    alert: <path d="M72 118 q18 -10 36 0" stroke={palette.accent} strokeWidth="7" strokeLinecap="round" fill="none" />,
    sad: <path d="M61 128 q29 -23 58 0" stroke={palette.accent} strokeWidth="7" strokeLinecap="round" fill="none" />,
    sleeping: <path d="M78 120 h24" stroke={palette.accent} strokeWidth="7" strokeLinecap="round" fill="none" />,
  }[mood]

  return (
    <svg viewBox="0 0 180 180" className="h-40 w-40 drop-shadow-[0_0_30px_rgba(34,211,238,0.18)]" role="img" aria-label={`Avatar do robô em estado ${mood}`}>
      <defs>
        <linearGradient id={`robot-shell-${mood}`} x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor={palette.shell} stopOpacity="0.95" />
          <stop offset="100%" stopColor={palette.accent} stopOpacity="0.75" />
        </linearGradient>
      </defs>
      <circle cx="90" cy="90" r="84" fill={`url(#robot-shell-${mood})`} opacity="0.14" />
      <rect x="44" y="28" width="92" height="28" rx="14" fill={palette.shell} opacity="0.9" />
      <circle cx="90" cy="24" r="8" fill={palette.eye} />
      <path d="M90 32 V16" stroke={palette.eye} strokeWidth="4" strokeLinecap="round" />
      <rect x="24" y="42" width="132" height="102" rx="34" fill="#e2e8f0" opacity="0.96" />
      <rect x="34" y="52" width="112" height="82" rx="28" fill={palette.face} />
      <rect x="16" y="68" width="20" height="48" rx="10" fill="#e2e8f0" opacity="0.92" />
      <rect x="144" y="68" width="20" height="48" rx="10" fill="#e2e8f0" opacity="0.92" />
      <rect x="56" y="144" width="18" height="24" rx="9" fill="#e2e8f0" opacity="0.9" />
      <rect x="106" y="144" width="18" height="24" rx="9" fill="#e2e8f0" opacity="0.9" />
      <circle cx="90" cy="140" r="10" fill={palette.accent} opacity="0.95" />
      <circle cx="52" cy="108" r="8" fill={palette.blush} opacity="0.28" />
      <circle cx="128" cy="108" r="8" fill={palette.blush} opacity="0.28" />
      {eyeMarkup}
      {mouthMarkup}
      {mood === 'alert' && <circle cx="90" cy="55" r="5" fill={palette.eye} opacity="0.95" />}
      {mood === 'sleeping' && (
        <>
          <text x="138" y="44" fill={palette.eye} fontSize="18" fontWeight="700">Z</text>
          <text x="149" y="32" fill={palette.eye} fontSize="13" fontWeight="700">Z</text>
        </>
      )}
      {mood === 'sad' && <path d="M126 96 q8 12 0 23" stroke={palette.eye} strokeWidth="3" strokeLinecap="round" fill="none" opacity="0.7" />}
    </svg>
  )
}

export function HealthSentinelCard({ mood, title, subtitle, badgeClass, activeCalls, sseConnected, latencyMs, aiStatus, overallStatus }) {
  return (
    <section className="glass-card overflow-hidden border border-slate-800/80 p-0">
      <div className="grid grid-cols-1 lg:grid-cols-[320px_1fr]">
        <div className="flex items-center justify-center bg-[radial-gradient(circle_at_top,rgba(56,189,248,0.18),transparent_58%),linear-gradient(180deg,rgba(15,23,42,0.95),rgba(2,6,23,1))] px-8 py-8">
          <RobotStatusAvatar mood={mood} />
        </div>
        <div className="p-5 sm:p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-xs uppercase tracking-[0.22em] text-slate-500">Sentinela operacional</p>
              <h2 className="mt-2 text-2xl font-semibold text-slate-100">{title}</h2>
              <p className="mt-2 max-w-2xl text-sm text-slate-400">{subtitle}</p>
            </div>
            <span className={`rounded-full border px-3 py-1 text-xs font-medium ${badgeClass}`}>
              {sseConnected ? 'SSE conectado' : 'SSE sem comunicação'}
            </span>
          </div>
          <div className="mt-5 grid grid-cols-2 gap-3 xl:grid-cols-4">
            <div className="rounded-2xl border border-slate-800 bg-slate-950/50 p-3">
              <p className="text-xs uppercase tracking-wide text-slate-500">Status geral</p>
              <p className="mt-2 text-base font-semibold text-slate-100 capitalize">{formatStatus(overallStatus)}</p>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/50 p-3">
              <p className="text-xs uppercase tracking-wide text-slate-500">IA</p>
              <p className="mt-2 text-base font-semibold text-slate-100 capitalize">{formatStatus(aiStatus)}</p>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/50 p-3">
              <p className="text-xs uppercase tracking-wide text-slate-500">Chamadas ativas</p>
              <p className="mt-2 text-base font-semibold text-slate-100">{activeCalls}</p>
            </div>
            <div className="rounded-2xl border border-slate-800 bg-slate-950/50 p-3">
              <p className="text-xs uppercase tracking-wide text-slate-500">Latência IA</p>
              <p className="mt-2 text-base font-semibold text-slate-100">{prettyMs(latencyMs)}</p>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}