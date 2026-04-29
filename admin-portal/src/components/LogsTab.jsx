import { ConversationsPanel } from './ConversationsPanel'
import { LiveEventsPanel, RecentEventsPanel } from './EventsPanel'

export function LogsTab({
  sseConnected,
  realtimeEvents,
  recentEvents,
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
    <main className="space-y-5">
      <LiveEventsPanel sseConnected={sseConnected} realtimeEvents={realtimeEvents} />

      <RecentEventsPanel recentEvents={recentEvents} />

      <ConversationsPanel
        clients={clients}
        conversationFilters={conversationFilters}
        conversationsQuery={conversationsQuery}
        serverRiskLabel={serverRiskLabel}
        visibleConversationSessions={visibleConversationSessions}
        onFilterChange={onFilterChange}
        onSearch={onSearch}
        onToggleHighRisk={onToggleHighRisk}
        onPreviousPage={onPreviousPage}
        onNextPage={onNextPage}
        onExportJson={onExportJson}
        onExportCsv={onExportCsv}
      />
    </main>
  )
}