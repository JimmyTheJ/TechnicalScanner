import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { BreadthPage } from './pages/BreadthPage'
import { OverviewPage } from './pages/OverviewPage'
import { ScreenerPage } from './pages/ScreenerPage'
import { StockDetailPage } from './pages/StockDetailPage'
import { WatchlistsPage } from './pages/WatchlistsPage'
import { ProfilesPage } from './pages/ProfilesPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<OverviewPage />} />
          <Route path="screener" element={<ScreenerPage />} />
          <Route path="profiles" element={<ProfilesPage />} />
          <Route path="breadth" element={<BreadthPage />} />
          <Route path="watchlists" element={<WatchlistsPage />} />
          <Route path="stocks/:symbol" element={<StockDetailPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}
