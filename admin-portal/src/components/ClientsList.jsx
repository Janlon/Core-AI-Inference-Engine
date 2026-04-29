import { Building2, Edit2, Plus, Trash2 } from 'lucide-react'

export function ClientsList({ clients, onEdit, onDelete, onNew }) {
  return (
    <section className="glass-card">
      <div className="mb-4 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-base font-semibold">
          <Building2 className="h-5 w-5 text-cyan-300" />
          Clientes Cadastrados ({clients.length})
        </h3>
        <button
          onClick={onNew}
          className="inline-flex items-center gap-2 rounded-lg bg-cyan-600 px-3 py-1.5 text-xs font-medium text-slate-100 hover:bg-cyan-500 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Novo
        </button>
      </div>
      <div className="space-y-2">
        {clients.length === 0 ? (
          <p className="py-6 text-center text-sm text-slate-400">Nenhum cliente cadastrado.</p>
        ) : (
          clients.map((client) => (
            <article key={client.id} className="rounded-xl border border-slate-800 bg-slate-900/40 p-3 hover:border-slate-700 transition-colors">
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-slate-100 truncate">{client.nomeIdentificador}</p>
                  <p className="text-xs text-slate-400 truncate">PID {client.pid} • {client.tipoLocal} • {client.systemType}</p>
                  <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-400">
                    <span className={`rounded-full px-2 py-0.5 ${client.isActive ? 'bg-emerald-500/15 text-emerald-300' : 'bg-slate-700 text-slate-300'}`}>
                      {client.isActive ? 'Ativo' : 'Inativo'}
                    </span>
                    {client.ramalTransfHumano && <span>Ramal humano: {client.ramalTransfHumano}</span>}
                    {client.sipTrunkPrefix && <span>SIP: {client.sipTrunkPrefix}</span>}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => onEdit(client)}
                    className="rounded-lg bg-slate-800 p-2 hover:bg-slate-700 transition-colors text-cyan-300"
                    title="Editar"
                  >
                    <Edit2 className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => onDelete(client.id)}
                    className="rounded-lg bg-slate-800 p-2 hover:bg-rose-900/40 transition-colors text-rose-400"
                    title="Deletar"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </article>
          ))
        )}
      </div>
    </section>
  )
}