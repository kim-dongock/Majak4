import { Outlet, useNavigate, useLocation } from 'react-router-dom'
import { Layout, Menu, Avatar, Dropdown, Typography, Space, Badge } from 'antd'
import {
  DashboardOutlined, UserOutlined, GiftOutlined, DatabaseOutlined,
  NotificationOutlined, SettingOutlined, LogoutOutlined, ClockCircleOutlined,
} from '@ant-design/icons'
import { useAuthStore } from '../store/authStore'
import { useQuery } from '@tanstack/react-query'
import { dashboardApi } from '../api/admin'

const { Header, Sider, Content } = Layout
const { Text } = Typography

export default function AdminLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const { email, role, clearAuth } = useAuthStore()

  const { data: stats } = useQuery({
    queryKey: ['dashboard'],
    queryFn: () => dashboardApi.getStats(),
    refetchInterval: 60_000,
  })
  const pendingCount = stats?.pendingApproval ?? 0

  const menuItems = [
    {
      key: '/dashboard',
      icon: <DashboardOutlined />,
      label: 'ダッシュボード',
    },
    {
      key: 'users',
      icon: <UserOutlined />,
      label: 'ユーザー管理',
      children: [
        { key: '/users', label: 'ユーザー検索' },
        {
          key: '/users/pending',
          label: (
            <Space>
              承認待ち
              {pendingCount > 0 && (
                <Badge count={pendingCount} overflowCount={99} size="small" />
              )}
            </Space>
          ),
          icon: <ClockCircleOutlined />,
        },
      ],
    },
    {
      key: 'gem',
      icon: <GiftOutlined />,
      label: 'GEM管理',
      children: [
        { key: '/gem/adjust', label: 'GEM 支給・調整' },
        { key: '/gem/stats',  label: 'GEM 統計・売上' },
      ],
    },
    {
      key: 'master',
      icon: <DatabaseOutlined />,
      label: 'マスターデータ',
      children: [
        { key: '/master/gem-products', label: 'GEM商品マスター' },
        { key: '/master/channels',     label: 'チャンネル一覧' },
      ],
    },
    {
      key: 'operations',
      icon: <NotificationOutlined />,
      label: '運営ツール',
      children: [
        { key: '/operations/notice', label: '公知送信' },
      ],
    },
    {
      key: 'settings',
      icon: <SettingOutlined />,
      label: '設定',
      children: [
        { key: '/settings/accounts', label: '管理者アカウント' },
      ],
    },
  ]

  const userMenuItems = [
    {
      key: 'logout',
      icon: <LogoutOutlined />,
      label: 'ログアウト',
      onClick: () => { clearAuth(); navigate('/login') },
    },
  ]

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider
        theme="dark"
        collapsible
        width={220}
        style={{ position: 'fixed', height: '100vh', left: 0, zIndex: 100, overflowY: 'auto' }}
      >
        <div style={{
          height: 48, display: 'flex', alignItems: 'center',
          justifyContent: 'center', color: '#fff', fontWeight: 700,
          fontSize: 16, borderBottom: '1px solid #303030',
        }}>
          🀄 Majak4 Admin
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          defaultOpenKeys={['users', 'gem', 'master', 'operations', 'settings']}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{ marginTop: 8 }}
        />
      </Sider>

      <Layout style={{ marginLeft: 220 }}>
        <Header style={{
          background: '#fff', padding: '0 24px',
          display: 'flex', alignItems: 'center', justifyContent: 'flex-end',
          borderBottom: '1px solid #f0f0f0', position: 'sticky', top: 0, zIndex: 99,
        }}>
          <Dropdown menu={{ items: userMenuItems }} placement="bottomRight">
            <Space style={{ cursor: 'pointer' }}>
              <Avatar icon={<UserOutlined />} size="small" />
              <Text style={{ maxWidth: 200 }} ellipsis>{email}</Text>
              <Text type="secondary" style={{ fontSize: 12 }}>({role})</Text>
            </Space>
          </Dropdown>
        </Header>

        <Content style={{ margin: 24, minHeight: 'calc(100vh - 112px)' }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
