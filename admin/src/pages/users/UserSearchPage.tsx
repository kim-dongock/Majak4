import { useState } from 'react'
import { Table, Input, Button, Space, Tag, Typography, Card } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { userApi } from '../../api/admin'
import type { PlayerSummary } from '../../api/types'
import dayjs from 'dayjs'

const { Title } = Typography

export default function UserSearchPage() {
  const navigate = useNavigate()
  const [keyword, setKeyword] = useState('')
  const [data, setData]       = useState<PlayerSummary[]>([])
  const [loading, setLoading] = useState(false)
  const [searched, setSearched] = useState(false)

  const search = async () => {
    setLoading(true)
    try {
      const result = await userApi.search(keyword || undefined)
      setData(result)
      setSearched(true)
    } finally {
      setLoading(false)
    }
  }

  const columns = [
    {
      title: '表示名',
      dataIndex: 'displayName',
      render: (_: string, record: PlayerSummary) => (
        <a onClick={() => navigate(`/users/${record.memberNo}`)}>{record.displayName}</a>
      ),
    },
    { title: 'アバター', dataIndex: 'avatarId' },
    { title: '会員番号', dataIndex: 'memberNo', width: 90 },
    {
      title: '状態',
      dataIndex: 'accountStatus',
      render: (s: number) =>
        s === 1 ? <Tag color="green">有効</Tag> : <Tag color="red">停止</Tag>,
    },
    {
      title: 'ゲームマネー',
      dataIndex: 'gameMoney',
      render: (v: number) => v.toLocaleString(),
    },
    {
      title: '龍珠',
      dataIndex: 'gemCount',
      render: (v: number) => <Tag color="purple">{v.toLocaleString()}</Tag>,
    },
    {
      title: 'キャッシュ',
      dataIndex: 'cashCount',
      render: (v: number) => <Tag color="blue">{v.toLocaleString()}</Tag>,
    },
    {
      title: '最終ログイン',
      dataIndex: 'lastLoginAt',
      render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm'),
    },
    {
      title: '',
      render: (_: unknown, record: PlayerSummary) => (
        <Button size="small" onClick={() => navigate(`/users/${record.memberNo}`)}>
          詳細
        </Button>
      ),
    },
  ]

  return (
    <>
      <Title level={4}>ユーザー検索</Title>
      <Card style={{ marginBottom: 16 }}>
        <Space>
          <Input
            placeholder="会員番号 または 表示名"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onPressEnter={search}
            style={{ width: 300 }}
            prefix={<SearchOutlined />}
          />
          <Button type="primary" onClick={search} loading={loading}>
            検索
          </Button>
        </Space>
      </Card>
      {searched && (
        <Table
          rowKey="memberNo"
          dataSource={data}
          columns={columns}
          loading={loading}
          pagination={{ pageSize: 30 }}
          size="small"
        />
      )}
    </>
  )
}
