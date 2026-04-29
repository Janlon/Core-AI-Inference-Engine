import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'

export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''

async function fetchJson(url) {
  const response = await fetch(url)
  if (!response.ok) throw new Error(`Erro ${response.status} em ${url}`)
  return response.json()
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

export function useHealthSnapshot() {
  return useQuery({
    queryKey: ['health'],
    queryFn: async () => fetchJson(`${API_BASE}/api/health`),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  })
}

export function useClientsList() {
  return useQuery({
    queryKey: ['clients'],
    queryFn: async () => fetchJson(`${API_BASE}/api/tenants?includeInactive=true`),
    refetchInterval: 30_000,
  })
}

export function useRecentEvents() {
  return useQuery({
    queryKey: ['events', 'recent'],
    queryFn: async () => {
      const payload = await fetchJson(`${API_BASE}/api/events?limit=20`)
      return payload.events ?? []
    },
    refetchInterval: false,
  })
}

export function useConversationThreads(filters) {
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

export function useLiveEventsSSE() {
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