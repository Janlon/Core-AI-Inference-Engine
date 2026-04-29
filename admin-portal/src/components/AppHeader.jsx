export function AppHeader() {
  return (
    <header className="mb-6 rounded-2xl border border-cyan-400/20 bg-gradient-to-r from-slate-900 via-slate-900 to-cyan-950/40 p-5 shadow-lg shadow-cyan-900/20">
      <p className="text-xs uppercase tracking-[0.22em] text-cyan-300">Portaria Virtual SaaS</p>
      <h1 className="mt-2 text-2xl font-semibold sm:text-3xl">Console Administrativo Multi-Client</h1>
      <p className="mt-2 text-sm text-slate-400">Monitoramento de saude, configuracoes de dominios e eventos em tempo real.</p>
    </header>
  )
}