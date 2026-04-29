import { useState } from 'react'

const emptyClientForm = {
  pid: '',
  nomeIdentificador: '',
  tipoLocal: 'RESIDENCIAL',
  systemType: 'condominio',
  webhookUrl: '',
  apiToken: '',
  sipTrunkPrefix: '',
  ramalTransfHumano: '',
  usaBloco: false,
  usaTorre: false,
  recordingEnabled: false,
  isActive: true,
}

function toClientForm(client) {
  return {
    pid: String(client.pid),
    nomeIdentificador: client.nomeIdentificador,
    tipoLocal: client.tipoLocal,
    systemType: client.systemType,
    webhookUrl: client.webhookUrl,
    apiToken: client.apiToken,
    sipTrunkPrefix: client.sipTrunkPrefix,
    ramalTransfHumano: client.ramalTransfHumano,
    usaBloco: Boolean(client.usaBloco),
    usaTorre: Boolean(client.usaTorre),
    recordingEnabled: Boolean(client.recordingEnabled),
    isActive: Boolean(client.isActive),
  }
}

export function useClientManagement({ apiBase, clientsQuery }) {
  const [showClientForm, setShowClientForm] = useState(false)
  const [editingClient, setEditingClient] = useState(null)
  const [formData, setFormData] = useState(emptyClientForm)

  const closeClientForm = () => setShowClientForm(false)

  const handleNewClient = () => {
    setEditingClient(null)
    setFormData(emptyClientForm)
    setShowClientForm(true)
  }

  const handleEditClient = (client) => {
    setEditingClient(client)
    setFormData(toClientForm(client))
    setShowClientForm(true)
  }

  const handleDeleteClient = async (clientId) => {
    if (!confirm('Tem certeza que deseja deletar este cliente?')) return

    try {
      const response = await fetch(`${apiBase}/api/tenants/${clientId}`, { method: 'DELETE' })
      if (!response.ok) throw new Error(`Erro ${response.status}`)
      clientsQuery.refetch()
      alert('Cliente deletado com sucesso!')
    } catch (error) {
      console.error('Erro ao deletar cliente:', error)
      alert('Erro ao deletar cliente')
    }
  }

  const handleSaveClient = async (e) => {
    e.preventDefault()

    try {
      const url = editingClient ? `${apiBase}/api/tenants/${editingClient.id}` : `${apiBase}/api/tenants`
      const method = editingClient ? 'PUT' : 'POST'
      const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          pid: Number(formData.pid),
          nomeIdentificador: formData.nomeIdentificador,
          tipoLocal: formData.tipoLocal,
          systemType: formData.systemType,
          webhookUrl: formData.webhookUrl || null,
          apiToken: formData.apiToken || null,
          sipTrunkPrefix: formData.sipTrunkPrefix || null,
          ramalTransfHumano: formData.ramalTransfHumano || null,
          usaBloco: formData.usaBloco,
          usaTorre: formData.usaTorre,
          recordingEnabled: formData.recordingEnabled,
          isActive: formData.isActive,
        }),
      })

      if (!response.ok) throw new Error(`Erro ${response.status}`)

      setShowClientForm(false)
      clientsQuery.refetch()
      alert(`Cliente ${editingClient ? 'atualizado' : 'criado'} com sucesso!`)
    } catch (error) {
      console.error('Erro ao salvar cliente:', error)
      alert('Erro ao salvar cliente')
    }
  }

  const handleFormChange = (e) => {
    const { name, value, type, checked } = e.target
    setFormData((prev) => ({ ...prev, [name]: type === 'checkbox' ? checked : value }))
  }

  return {
    showClientForm,
    editingClient,
    formData,
    closeClientForm,
    handleNewClient,
    handleEditClient,
    handleDeleteClient,
    handleSaveClient,
    handleFormChange,
  }
}