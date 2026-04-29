export function SummaryCards({ items }) {
  return (
    <section className="mb-6 grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-6">
      {items.map((item) => (
        <article key={item.label} className="glass-card flex items-center justify-between">
          <div>
            <p className="text-xs uppercase tracking-wide text-slate-400">{item.label}</p>
            <p className="mt-1 text-lg font-semibold text-slate-100">{item.value}</p>
          </div>
          <item.icon className="h-5 w-5 text-cyan-300" />
        </article>
      ))}
    </section>
  )
}