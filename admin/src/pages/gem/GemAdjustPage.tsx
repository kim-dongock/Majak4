import { useState } from 'react'
import {
  Card, Form, Input, InputNumber, Button, Alert, Typography, Space, Divider, List, Tag, Select,
} from 'antd'
import { useSearchParams } from 'react-router-dom'
import { currencyApi, userApi } from '../../api/admin'
import type { PlayerSummary } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title, Text } = Typography

export default function GemAdjustPage() {
  const canManageGem = useAuthStore((s: { canManageGem: () => boolean }) => s.canManageGem())
  const [searchParams] = useSearchParams()
  const [form] = Form.useForm()

  const [searching, setSearching] = useState(false)
  const [searchKeyword, setSearchKeyword] = useState(searchParams.get('memberNo') ?? '')
  const [searchResults, setSearchResults] = useState<PlayerSummary[]>([])
  const [player, setPlayer]     = useState<PlayerSummary | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult]     = useState<{
    currency: 'gp' | 'mp' | 'dragon_orb'
    balanceBefore: number
    balanceAfter: number
  } | null>(null)
  const [error, setError]       = useState<string | null>(null)

  const defaultMemberNo = searchParams.get('memberNo') ?? ''

  const searchPlayers = async () => {
    const keyword = searchKeyword.trim()
    if (!keyword) return
    setSearching(true)
    setPlayer(null)
    setSearchResults([])
    form.setFieldValue('memberNo', undefined)
    setResult(null)
    setError(null)
    try {
      const players = await userApi.search(keyword, 0, 20)
      setSearchResults(players)
      if (players.length === 0) setError('プレイヤーが見つかりません')
    } catch {
      setError('プレイヤーの検索に失敗しました')
    } finally {
      setSearching(false)
    }
  }

  const selectPlayer = (selectedPlayer: PlayerSummary) => {
    setPlayer(selectedPlayer)
    form.setFieldValue('memberNo', String(selectedPlayer.memberNo))
    setResult(null)
    setError(null)
  }

  const handleSubmit = async (values: { memberNo: string; currency: 'gp' | 'mp' | 'dragon_orb'; amount: number; memo: string }) => {
    setSubmitting(true)
    setResult(null)
    setError(null)
    try {
      const res = await currencyApi.adjust(Number(values.memberNo), values.currency, values.amount, values.memo)
      setResult(res)
      setPlayer(prev => prev ? {
        ...prev,
        gameMoney: values.currency === 'gp' ? res.balanceAfter : prev.gameMoney,
        gemCount: values.currency === 'dragon_orb' ? res.balanceAfter : prev.gemCount,
        cashCount: values.currency === 'mp' ? res.balanceAfter : prev.cashCount,
      } : prev)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  if (!canManageGem) {
    return <Alert type="error" message="この操作は Operator 以上のみ実行できます" />
  }

  return (
    <>
      <Title level={4}>通貨 支給・調整</Title>
      <Card style={{ maxWidth: 560 }}>
        <Space.Compact style={{ width: '100%', marginBottom: 16 }}>
          <Input
            placeholder="会員番号 または 表示名"
            value={searchKeyword}
            onChange={(event) => setSearchKeyword(event.target.value)}
            onPressEnter={searchPlayers}
          />
          <Button type="primary" loading={searching} onClick={searchPlayers}>
            検索
          </Button>
        </Space.Compact>

        {searchResults.length > 0 && (
          <List
            size="small"
            bordered
            dataSource={searchResults}
            style={{ marginBottom: 16 }}
            renderItem={(candidate) => (
              <List.Item
                actions={[
                  <Button
                    key={candidate.memberNo}
                    size="small"
                    type={player?.memberNo === candidate.memberNo ? 'primary' : 'default'}
                    onClick={() => selectPlayer(candidate)}
                  >
                    選択
                  </Button>,
                ]}
              >
                <Space>
                  <Text strong>{candidate.displayName}</Text>
                  <Text type="secondary">#{candidate.memberNo}</Text>
                  <Tag color="gold">{candidate.gameMoney.toLocaleString()} GP</Tag>
                  <Tag color="blue">{candidate.cashCount.toLocaleString()} MP</Tag>
                  <Tag color="purple">{candidate.gemCount.toLocaleString()} 龍珠</Tag>
                </Space>
              </List.Item>
            )}
          />
        )}

        <Form
          form={form}
          layout="vertical"
          initialValues={{ memberNo: defaultMemberNo, currency: 'gp', amount: 0 }}
          onFinish={handleSubmit}
        >
          <Form.Item name="memberNo" rules={[{ required: true, message: 'プレイヤーを選択してください' }]} hidden>
            <Input />
          </Form.Item>

          {player && (
            <Card size="small" style={{ marginBottom: 16, background: '#fafafa' }}>
              <Space>
                <Text strong>{player.displayName}</Text>
                <Text type="secondary">#{player.memberNo}</Text>
                <Text strong style={{ color: '#ad6800' }}>{player.gameMoney.toLocaleString()} GP</Text>
                <Text strong style={{ color: '#1677ff' }}>{player.cashCount.toLocaleString()} MP</Text>
                <Text strong style={{ color: '#722ed1' }}>{player.gemCount.toLocaleString()} 龍珠</Text>
              </Space>
            </Card>
          )}

          <Form.Item label="通貨" name="currency" rules={[{ required: true }]}>
            <Select options={[
              { value: 'gp', label: 'GP' },
              { value: 'mp', label: 'MP' },
              { value: 'dragon_orb', label: '龍珠' },
            ]} />
          </Form.Item>

          <Form.Item
            label="調整量 (正数: 支給 / 負数: 回収)"
            name="amount"
            rules={[
              { required: true },
              { type: 'number', min: -99999, max: 99999 },
              { validator: (_, v) => v !== 0 ? Promise.resolve() : Promise.reject('0 以外を入力してください') },
            ]}
          >
            <InputNumber
              style={{ width: '100%' }}
              min={-99999}
              max={99999}
              formatter={(v) => `${v}`}
            />
          </Form.Item>

          <Form.Item
            label="理由 (必須)"
            name="memo"
            rules={[{ required: true, min: 5, message: '5文字以上の理由を入力してください' }]}
          >
            <Input.TextArea rows={3} placeholder="例: イベント補償、誤請求返金など" />
          </Form.Item>

          <Divider />

          <Button type="primary" danger htmlType="submit" loading={submitting} disabled={!player}>
            実行
          </Button>
        </Form>

        {result && (
          <Alert
            style={{ marginTop: 16 }}
            type="success"
            message={`完了: ${result.balanceBefore.toLocaleString()} ${result.currency === 'gp' ? 'GP' : result.currency === 'mp' ? 'MP' : '龍珠'} → ${result.balanceAfter.toLocaleString()} ${result.currency === 'gp' ? 'GP' : result.currency === 'mp' ? 'MP' : '龍珠'}`}
          />
        )}
        {error && (
          <Alert style={{ marginTop: 16 }} type="error" message={error} />
        )}
      </Card>
    </>
  )
}
