import { useState } from 'react'
import { ChevronDown, ChevronUp } from 'lucide-react'

function prettyMs(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'
  return `${Math.round(Number(value))} ms`
}

function prettyDateTime(value) {
  if (!value) return '--'
  return new Date(value).toLocaleString()
}

function buildFinalSummaryText(session) {
  const finalActionLabel = session.finalAction ?? 'CHAMADA_ENCERRADA'
  const finalReasonMessage = session.finalReasonMessage ? ` Motivo: ${session.finalReasonMessage}` : ''
  const webhook = session.finalNotification?.webhook
  const webhookDetail = webhook?.httpStatusCode != null
    ? ` HTTP ${webhook.httpStatusCode}.`
    : webhook?.elapsedMs != null
      ? ` Tempo webhook: ${prettyMs(webhook.elapsedMs)}.`
      : ''

  return `Atendimento encerrado. Ação final: ${finalActionLabel}.${finalReasonMessage}${webhookDetail}`.trim()
}

export function ConversationCard({ session }) {
  const [isExpanded, setIsExpanded] = useState(false)

  const telemetry = session.telemetry ?? {}
  const loopTone = telemetry.loopRisk === 'high'
    ? 'bg-rose-500/15 text-rose-300 border-rose-500/30'
    : telemetry.loopRisk === 'medium'
      ? 'bg-amber-500/15 text-amber-300 border-amber-500/30'
      : 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30'

  const endedAtLabel = session.endedAt ? new Date(session.endedAt).toLocaleString() : null
  const finalLayerLabel = session.finalResolutionLayer ?? 'N/D'
  const finalReasonLabel = session.finalReasonMessage ?? session.finalReasonCode ?? 'N/D'
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
      text: buildFinalSummaryText(session),
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
              <span className="rounded-full border border-amber-700/40 bg-amber-950/20 px-2 py-0.5 text-amber-200">Motivo final: {finalReasonLabel}</span>
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
            {session.finalNotification?.webhook?.httpStatusCode != null && <span>HTTP webhook: {session.finalNotification.webhook.httpStatusCode}</span>}
            {session.finalNotification?.webhook?.elapsedMs != null && <span>Tempo webhook: {prettyMs(session.finalNotification.webhook.elapsedMs)}</span>}
            {session.finalNotification?.webhook?.payloadSentAtUtc && <span>Envio webhook: {prettyDateTime(session.finalNotification.webhook.payloadSentAtUtc)}</span>}
            {session.finalNotification?.webhook?.correlationId && <span>Correlação externa: {session.finalNotification.webhook.correlationId}</span>}
          </footer>
          {session.finalNotification?.webhook?.responseBodyExcerpt && (
            <div className="mt-2 rounded-xl border border-slate-800 bg-slate-950/50 px-3 py-2 text-[11px] text-slate-400">
              <p className="text-slate-500">Retorno do webhook</p>
              <p className="mt-1 whitespace-pre-wrap break-words">{session.finalNotification.webhook.responseBodyExcerpt}</p>
            </div>
          )}
          {(session.finalNotification?.webhook?.payloadHash || session.finalNotification?.webhook?.correlationField) && (
            <div className="mt-2 rounded-xl border border-slate-800 bg-slate-950/50 px-3 py-2 text-[11px] text-slate-400">
              <p className="text-slate-500">Prova de entrega</p>
              {session.finalNotification?.webhook?.payloadHash && <p className="mt-1 break-all">Hash payload: {session.finalNotification.webhook.payloadHash}</p>}
              {session.finalNotification?.webhook?.correlationField && <p className="mt-1">Campo de correlação: {session.finalNotification.webhook.correlationField}</p>}
            </div>
          )}
        </>
      )}
    </article>
  )
}