import { Building2, X } from 'lucide-react'

export function ClientFormModal({ editingClient, formData, onClose, onSubmit, onChange }) {
  return (
    <section className="glass-card">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="flex items-center gap-2 text-lg font-semibold">
          <Building2 className="h-5 w-5 text-cyan-300" />
          {editingClient ? 'Editar Cliente' : 'Novo Cliente'}
        </h2>
        <button
          onClick={onClose}
          className="rounded-lg bg-slate-800 p-1.5 hover:bg-slate-700 transition-colors"
        >
          <X className="h-5 w-5 text-slate-400" />
        </button>
      </div>
      <form className="grid grid-cols-1 gap-3 sm:grid-cols-2" onSubmit={onSubmit}>
        <input
          className="field"
          placeholder="PID do tenant"
          name="pid"
          value={formData.pid}
          onChange={onChange}
          required
          type="number"
          min="1"
        />
        <input
          className="field"
          placeholder="Nome do condominio"
          name="nomeIdentificador"
          value={formData.nomeIdentificador}
          onChange={onChange}
          required
        />
        <select
          className="field"
          name="tipoLocal"
          value={formData.tipoLocal}
          onChange={onChange}
          required
        >
          <option value="RESIDENCIAL">Residencial</option>
          <option value="COMERCIAL">Comercial</option>
          <option value="HOSPITALAR">Hospitalar</option>
          <option value="INDUSTRIAL">Industrial</option>
        </select>
        <input
          className="field"
          placeholder="System Type"
          name="systemType"
          value={formData.systemType}
          onChange={onChange}
          required
        />
        <input
          className="field"
          placeholder="Webhook URL"
          name="webhookUrl"
          value={formData.webhookUrl}
          onChange={onChange}
        />
        <textarea
          className="field sm:col-span-2"
          placeholder="API token do tenant"
          name="apiToken"
          value={formData.apiToken}
          onChange={onChange}
          rows={3}
        />
        <div className="sm:col-span-2 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <input
            className="field"
            placeholder="Sip Trunk Prefix"
            name="sipTrunkPrefix"
            value={formData.sipTrunkPrefix}
            onChange={onChange}
          />
          <input
            className="field"
            placeholder="Ramal de transferencia humana"
            name="ramalTransfHumano"
            value={formData.ramalTransfHumano}
            onChange={onChange}
          />
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input type="checkbox" name="usaBloco" checked={formData.usaBloco} onChange={onChange} />
          Usa bloco
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input type="checkbox" name="usaTorre" checked={formData.usaTorre} onChange={onChange} />
          Usa torre
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input type="checkbox" name="recordingEnabled" checked={formData.recordingEnabled} onChange={onChange} />
          Grava chamadas
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-300">
          <input type="checkbox" name="isActive" checked={formData.isActive} onChange={onChange} />
          Tenant ativo
        </label>
        <div className="sm:col-span-2 flex items-center gap-3">
          <button className="btn-primary flex-1" type="submit">
            {editingClient ? 'Atualizar cliente' : 'Criar novo cliente'}
          </button>
          <button
            className="flex-1 rounded-lg border border-slate-700 bg-slate-800/50 px-4 py-2 font-medium text-slate-300 hover:border-slate-600 hover:bg-slate-700/50 transition-colors"
            type="button"
            onClick={onClose}
          >
            Cancelar
          </button>
        </div>
      </form>
    </section>
  )
}