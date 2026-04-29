import { useMemo, useState } from 'react'
import { exportConversationsCsv, exportConversationsJson } from '../lib/conversationExports'
import { useConversationThreads } from './usePortalData'

const initialConversationFilters = {
  tenantId: '',
  keyword: '',
  sessionId: '',
  loopRisk: '',
  fromUtc: '',
  toUtc: '',
  limit: 20,
  offset: 0,
}

export function useConversationManagement() {
  const [conversationFilters, setConversationFilters] = useState(initialConversationFilters)
  const conversationsQuery = useConversationThreads(conversationFilters)

  const visibleConversationSessions = conversationsQuery.data?.sessions ?? []
  const serverRiskLabel = conversationFilters.loopRisk
    ? `Filtro risco: ${conversationFilters.loopRisk.toUpperCase()}`
    : 'Filtro risco: TODOS'

  const handleConversationFilterChange = (e) => {
    const { name, value } = e.target
    setConversationFilters((prev) => ({ ...prev, [name]: value, offset: 0 }))
  }

  const handleConversationSearch = (e) => {
    e.preventDefault()
    conversationsQuery.refetch()
  }

  const handleConversationPage = (direction) => {
    const limit = Number(conversationFilters.limit ?? 20)
    setConversationFilters((prev) => {
      const currentOffset = Number(prev.offset ?? 0)
      const nextOffset = direction === 'next'
        ? currentOffset + limit
        : Math.max(0, currentOffset - limit)

      return { ...prev, offset: nextOffset }
    })
  }

  const handleToggleHighRisk = (e) => {
    setConversationFilters((prev) => ({
      ...prev,
      loopRisk: e.target.checked ? 'high' : '',
      offset: 0,
    }))
  }

  const handleExportConversationsJson = () => {
    exportConversationsJson({
      sessions: visibleConversationSessions,
      filters: conversationFilters,
    })
  }

  const handleExportConversationsCsv = () => {
    exportConversationsCsv({ sessions: visibleConversationSessions })
  }

  return useMemo(() => ({
    conversationFilters,
    conversationsQuery,
    visibleConversationSessions,
    serverRiskLabel,
    handleConversationFilterChange,
    handleConversationSearch,
    handleConversationPage,
    handleToggleHighRisk,
    handleExportConversationsJson,
    handleExportConversationsCsv,
  }), [
    conversationFilters,
    conversationsQuery,
    visibleConversationSessions,
    serverRiskLabel,
  ])
}