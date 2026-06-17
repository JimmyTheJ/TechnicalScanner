import { NavLink, Outlet } from 'react-router-dom'

const links = [
  { to: '/', label: 'Overview' },
  { to: '/screener', label: 'Screener' },
  { to: '/breadth', label: 'Breadth' },
  { to: '/watchlists', label: 'Watchlists' },
]

export function Layout() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <div>
          <p className="eyebrow">Technical Scanner</p>
          <h1>Market Technical Dashboard</h1>
        </div>
        <nav className="app-nav">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              className={({ isActive }) => (isActive ? 'nav-link active' : 'nav-link')}
              end={link.to === '/'}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
    </div>
  )
}
