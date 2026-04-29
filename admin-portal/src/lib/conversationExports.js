function downloadBlob(blob, extension) {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `conversas-${new Date().toISOString().replace(/[:.]/g, '-')}.${extension}`
  document.body.appendChild(anchor)
  anchor.click()
  document.body.removeChild(anchor)
  URL.revokeObjectURL(url)
}

export function exportConversationsJson({ sessions, filters }) {
  const payload = {
    exportedAt: new Date().toISOString(),
    filters: {
      ...filters,
    },
    totalCount: sessions.length,
    sessions,
  }

  const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json;charset=utf-8;' })
  downloadBlob(blob, 'json')
}

export function exportConversationsCsv({ sessions }) {
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
        message.text ?? '',
        message.at,
      ]

      const csvLine = values.map((value) => `"${String(value ?? '').replace(/"/g, '""')}"`).join(',')
      rows.push(csvLine)
    }
  }

  const blob = new Blob([rows.join('\n')], { type: 'text/csv;charset=utf-8;' })
  downloadBlob(blob, 'csv')
}