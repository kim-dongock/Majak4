import { useEffect, useState } from 'react'
import { Card, Select, Typography, Spin, Alert, Table, Tag } from 'antd'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer,
} from 'recharts'
import { cashApi } from '../../api/admin'
import type { DailyRevenue } from '../../api/types'

const { Title } = Typography

const PLATFORM_COLORS: Record<string, string> = {
  web:     '#1677ff',
  ios:     '#52c41a',
  android: '#fa8c16',
}

export default function GemStatsPage() {
  const [days, setDays]         = useState(30)
  const [data, setData]         = useState<DailyRevenue[]>([])
  const [loading, setLoading]   = useState(false)
  const [error, setError]       = useState<string | null>(null)

  useEffect(() => {
    setLoading(true)
    cashApi.getRevenue(days)
      .then(setData)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [days])

  // チャート用: 日付ごとにグループ化
  const chartData = Object.values(
    data.reduce<Record<string, Record<string, number | string>>>((acc, row) => {
      const d = row.revenueDate
      if (!acc[d]) acc[d] = { date: d, web: 0, ios: 0, android: 0 }
      acc[d][row.platform] = row.totalJpy
      return acc
    }, {})
  ).reverse()

  const columns = [
    { title: '日付', dataIndex: 'revenueDate', key: 'date' },
    {
      title: 'プラットフォーム', dataIndex: 'platform',
      render: (p: string) => <Tag color={PLATFORM_COLORS[p]}>{p}</Tag>,
    },
    { title: '件数', dataIndex: 'orderCount', render: (v: number) => v.toLocaleString() },
    { title: '売上 (円)', dataIndex: 'totalJpy', render: (v: number) => `¥${v.toLocaleString()}` },
    { title: 'キャッシュ', dataIndex: 'totalCash', render: (v: number) => v.toLocaleString() },
  ]

  return (
    <>
      <Title level={4}>キャッシュ 統計・売上</Title>

      <Card style={{ marginBottom: 16 }}>
        <Select
          value={days}
          onChange={setDays}
          options={[
            { value: 7,   label: '直近 7 日' },
            { value: 30,  label: '直近 30 日' },
            { value: 90,  label: '直近 90 日' },
            { value: 180, label: '直近 180 日' },
          ]}
          style={{ width: 160 }}
        />
      </Card>

      {loading && <Spin size="large" style={{ display: 'block', textAlign: 'center', marginTop: 40 }} />}
      {error   && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}

      {!loading && data.length > 0 && (
        <>
          <Card title="日別売上 (円)" style={{ marginBottom: 16 }}>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                <YAxis tickFormatter={(v) => `¥${(v as number).toLocaleString()}`} />
                <Tooltip formatter={(v) => `¥${(v as number).toLocaleString()}`} />
                <Legend />
                <Bar dataKey="web"     name="Web"     fill={PLATFORM_COLORS.web} />
                <Bar dataKey="ios"     name="iOS"     fill={PLATFORM_COLORS.ios} />
                <Bar dataKey="android" name="Android" fill={PLATFORM_COLORS.android} />
              </BarChart>
            </ResponsiveContainer>
          </Card>

          <Card title="詳細データ">
            <Table
              rowKey={(r) => `${r.revenueDate}-${r.platform}`}
              dataSource={data}
              columns={columns}
              size="small"
              pagination={{ pageSize: 50 }}
            />
          </Card>
        </>
      )}
    </>
  )
}
