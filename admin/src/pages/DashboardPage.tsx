import { useEffect, useState } from 'react'
import { Row, Col, Card, Statistic, Typography, Spin, Alert } from 'antd'
import {
  TeamOutlined, RiseOutlined, GiftOutlined, DollarOutlined,
} from '@ant-design/icons'
import { dashboardApi } from '../api/admin'
import type { DashboardStats } from '../api/types'

const { Title } = Typography

export default function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    dashboardApi.getStats()
      .then(setStats)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />
  if (error)  return <Alert type="error" message={error} style={{ marginTop: 24 }} />

  return (
    <>
      <Title level={4} style={{ marginBottom: 24 }}>ダッシュボード</Title>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="総プレイヤー数"
              value={stats?.totalPlayers ?? 0}
              prefix={<TeamOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="本日アクティブ (24h)"
              value={stats?.activePlayersToday ?? 0}
              prefix={<RiseOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="本日 GEM 充電"
              value={stats?.gemChargedToday ?? 0}
              suffix="GEM"
              prefix={<GiftOutlined />}
              valueStyle={{ color: '#722ed1' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="本日売上"
              value={stats?.revenueJpyToday ?? 0}
              prefix={<DollarOutlined />}
              suffix="円"
              valueStyle={{ color: '#fa8c16' }}
            />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]} style={{ marginTop: 16 }}>
        <Col xs={24} sm={12}>
          <Card>
            <Statistic
              title="今月 GEM 充電合計"
              value={stats?.gemChargedThisMonth ?? 0}
              suffix="GEM"
              prefix={<GiftOutlined />}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12}>
          <Card>
            <Statistic
              title="今月売上合計"
              value={stats?.revenueJpyThisMonth ?? 0}
              prefix={<DollarOutlined />}
              suffix="円"
              valueStyle={{ color: '#1677ff' }}
            />
          </Card>
        </Col>
      </Row>
    </>
  )
}
