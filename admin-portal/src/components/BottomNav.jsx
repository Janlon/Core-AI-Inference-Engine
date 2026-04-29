export function BottomNav({ tabs, activeTab, onChange }) {
  return (
    <nav className="fixed inset-x-0 bottom-0 z-50 border-t border-slate-800 bg-slate-950/95 backdrop-blur sm:left-1/2 sm:max-w-md sm:-translate-x-1/2 sm:rounded-t-2xl sm:border-x">
      <ul className="grid grid-cols-3">
        {tabs.map((tab) => {
          const active = tab.id === activeTab
          return (
            <li key={tab.id}>
              <button
                className={`flex w-full flex-col items-center gap-1 py-3 text-xs ${active ? 'text-cyan-300' : 'text-slate-400'}`}
                onClick={() => onChange(tab.id)}
              >
                <tab.icon className={`h-5 w-5 ${active ? 'text-cyan-300' : 'text-slate-500'}`} />
                {tab.label}
              </button>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}