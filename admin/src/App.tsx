import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { ConfigProvider, theme } from 'antd'
import jaJP from 'antd/locale/ja_JP'
import AdminLayout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import UserSearchPage from './pages/users/UserSearchPage'
import UserDetailPage from './pages/users/UserDetailPage'
import PendingApprovalPage from './pages/users/PendingApprovalPage'
import GemAdjustPage from './pages/gem/GemAdjustPage'
import GemStatsPage from './pages/gem/GemStatsPage'
import GemProductPage from './pages/master/GemProductPage'
import ChannelListPage from './pages/master/ChannelListPage'
import NoticePage from './pages/operations/NoticePage'
import AdminAccountPage from './pages/settings/AdminAccountPage'

export default function App() {
  return (
    <ConfigProvider locale={jaJP} theme={{ algorithm: theme.defaultAlgorithm }}>
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute />}>
            <Route element={<AdminLayout />}>
              <Route index element={<Navigate to="/dashboard" replace />} />
              <Route path="dashboard" element={<DashboardPage />} />
              {/* ユーザー管理 */}
              <Route path="users" element={<UserSearchPage />} />
              <Route path="users/pending" element={<PendingApprovalPage />} />
              <Route path="users/:memberNo" element={<UserDetailPage />} />
              {/* キャッシュ */}
              <Route path="cash/adjust" element={<GemAdjustPage />} />
              <Route path="cash/stats" element={<GemStatsPage />} />
              {/* マスターデータ */}
              <Route path="master/cash-products" element={<GemProductPage />} />
              <Route path="master/channels" element={<ChannelListPage />} />
              {/* 運営ツール */}
              <Route path="operations/notice" element={<NoticePage />} />
              {/* 設定 */}
              <Route path="settings/accounts" element={<AdminAccountPage />} />
            </Route>
          </Route>
          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  )
}
