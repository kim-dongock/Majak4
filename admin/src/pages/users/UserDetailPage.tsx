import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Card, Descriptions, Tag, Button, Typography, Spin, Alert, Space,
} from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { userApi } from '../../api/admin'
import type { PlayerDetail } from '../../api/types'
import dayjs from 'dayjs'

const { Title } = Typography

export default function UserDetailPage() {
  const { memberNo } = useParams<{ memberNo: string }>()
  const navigate = useNavigate()
  const [player, setPlayer] = useState<PlayerDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError]     = useState<string | null>(null)

  useEffect(() => {
    if (!memberNo) return
    userApi.getDetail(memberNo)
      .then(setPlayer)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [memberNo])

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />
  if (error)  return <Alert type="error" message={error} />
  if (!player) return <Alert type="warning" message="プレイヤーが見つかりません" />

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)}>戻る</Button>
        <Title level={4} style={{ margin: 0 }}>プレイヤー詳細</Title>
      </Space>

      <Card>
        <Descriptions bordered column={2} size="small">
          <Descriptions.Item label="表示名">{player.displayName}</Descriptions.Item>
          <Descriptions.Item label="アバター">{player.avatarId || '—'}</Descriptions.Item>
          <Descriptions.Item label="会員番号">{player.memberNo}</Descriptions.Item>
          <Descriptions.Item label="状態">
            {player.accountStatus === 1
              ? <Tag color="green">有効</Tag>
              : <Tag color="red">停止</Tag>}
          </Descriptions.Item>
          <Descriptions.Item label="性別">{player.sexCode}</Descriptions.Item>
          <Descriptions.Item label="ゲームマネー">
            {player.gameMoney.toLocaleString()} G
          </Descriptions.Item>
          <Descriptions.Item label="龍珠">
            <Tag color="purple">{player.gemCount.toLocaleString()} 個</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="キャッシュ">
            <Tag color="blue">{player.cashCount.toLocaleString()} (有償 {player.paidCashCount.toLocaleString()} / 無償 {player.freeCashCount.toLocaleString()})</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="レーティング">{player.commonRating}</Descriptions.Item>
          <Descriptions.Item label="経験値">{player.experience.toLocaleString()}</Descriptions.Item>
          <Descriptions.Item label="週間ポイント">{player.weeklyPoint}</Descriptions.Item>
          <Descriptions.Item label="初回ログイン">
            {dayjs(player.firstLoginAt).format('YYYY-MM-DD HH:mm')}
          </Descriptions.Item>
          <Descriptions.Item label="最終ログイン">
            {dayjs(player.lastLoginAt).format('YYYY-MM-DD HH:mm')}
          </Descriptions.Item>
          <Descriptions.Item label="最終対局">
            {player.lastPlayedAt ? dayjs(player.lastPlayedAt).format('YYYY-MM-DD HH:mm') : '—'}
          </Descriptions.Item>
        </Descriptions>
      </Card>

      <Space style={{ marginTop: 16 }}>
        <Button onClick={() => navigate(`/cash/adjust?memberNo=${player.memberNo}`)}>
          キャッシュ 支給・調整
        </Button>
      </Space>
    </>
  )
}
