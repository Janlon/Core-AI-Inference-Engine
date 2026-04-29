import { ClientFormModal } from './ClientFormModal'
import { ClientsList } from './ClientsList'

export function ClientsTab({
  clients,
  clientsQuery,
  onEdit,
  onDelete,
  onNew,
  showClientForm,
  editingClient,
  formData,
  onCloseForm,
  onSubmitForm,
  onChangeForm,
}) {
  return (
    <main className="space-y-5">
      <ClientsList
        clients={clients}
        onEdit={onEdit}
        onDelete={onDelete}
        onNew={onNew}
      />

      {showClientForm && (
        <ClientFormModal
          editingClient={editingClient}
          formData={formData}
          onClose={onCloseForm}
          onSubmit={onSubmitForm}
          onChange={onChangeForm}
        />
      )}

      {clientsQuery.isFetching && <p className="text-xs text-cyan-300">Atualizando lista de clientes...</p>}
      {clientsQuery.isError && <p className="text-xs text-rose-300">Erro ao carregar tenants reais do backend.</p>}
    </main>
  )
}