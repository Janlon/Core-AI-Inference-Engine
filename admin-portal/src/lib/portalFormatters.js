export function formatStatus(status) {
  if (!status) return 'desconhecido'
  return String(status).toLowerCase()
}

export function statusTone(status) {
  const normalized = formatStatus(status)
  if (normalized === 'healthy' || normalized === 'alive' || normalized === 'ok') return 'ok'
  if (normalized === 'degraded' || normalized === 'warning') return 'warn'
  return 'down'
}

export function prettyMs(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'
  return `${Math.round(Number(value))} ms`
}

export function prettyDateTime(value) {
  if (!value) return '--'
  return new Date(value).toLocaleString()
}

export function prettyPercent(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'
  return `${Number(value).toFixed(1)}%`
}

export function prettyBytes(value) {
  if (value == null || Number.isNaN(Number(value))) return '--'

  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = Number(value)
  let unitIndex = 0

  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024
    unitIndex += 1
  }

  const decimals = size >= 10 || unitIndex === 0 ? 0 : 1
  return `${size.toFixed(decimals)} ${units[unitIndex]}`
}

export function resourceStatus(usagePercent, fallbackStatus = 'unknown') {
  if (usagePercent == null || Number.isNaN(Number(usagePercent))) return fallbackStatus
  if (Number(usagePercent) >= 90) return 'degraded'
  return 'healthy'
}