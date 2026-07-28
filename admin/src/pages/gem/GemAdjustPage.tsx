import { useState } from 'react'
import {
  Card, Form, Input, InputNumber, Button, Alert, Typography, Space, Divider,
} from 'antd'
import { useSearchParams } from 'react-router-dom'
import { gemApi, userApi } from '../../api/admin'
import type { PlayerDetail } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title, Text } = Typography

export default function GemAdjustPage() {
  const isSuperAdmin = useAuthStore((s: { isSuperAdmin: () => boolean }) => s.isSuperAdmin())
  const [searchParams] = useSearchParams()
  const [form] = Form.useForm()

  const [searching, setSearching] = useState(false)
  const [player, setPlayer]     = useState<PlayerDetail | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [result, setResult]     = useState<{ balanceBefore: number; balanceAfter: number } | null>(null)
  const [error, setError]       = useState<string | null>(null)

  const defaultMemberNo = searchParams.get('memberNo') ?? ''

  const lookupPlayer = async (memberNo: string) => {
    if (!memberNo.trim()) return
    setSearching(true)
    setPlayer(null)
    setResult(null)
    setError(null)
    try {
      const p = await userApi.getDetail(memberNo.trim())
      setPlayer(p)
    } catch {
      setError('プレイヤーが見つかりません')
    } finally {
      setSearching(false)
    }
  }

  const handleSubmit = async (values: { memberNo: string; amount: number; memo: string }) => {
    setSubmitting(true)
    setResult(null)
    setError(null)
    try {
      const res = await gemApi.adjust(Number(values.memberNo), values.amount, values.memo)
      setResult(res)
    setPlayer(prev => prev ? { ...prev, gemCount: res.balanceAfter } : prev)
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSubmitting(false)
    }
  }

  if (!isSuperAdmin) {
    return <Alert type="error" message="この操作は Super Admin のみ実行できます" />
  }

  return (
    <>
      <Title level={4}>GEM 支給・調整</Title>
      <Card style={{ maxWidth: 560 }}>
        <Form
          form={form}
          layout="vertical"
          initialValues={{ memberNo: defaultMemberNo, amount: 0 }}
          onFinish={handleSubmit}
        >
          <Form.Item label="会員番号" name="memberNo" rules={[{ required: true }]}>
            <Space.Compact style={{ width: '100%' }}>
              <Input placeholder="会員番号" style={{ flex: 1 }} />
              <Button
                loading={searching}
                onClick={() => lookupPlayer(form.getFieldValue('memberNo'))}
              >
                検索
              </Button>
            </Space.Compact>
          </Form.Item>

          {player && (
            <Card size="small" style={{ marginBottom: 16, background: '#fafafa' }}>
              <Space>
                <Text strong>{player.displayName}</Text>
                <Text type="secondary">#{player.memberNo}</Text>
                <Text>現在 GEM: </Text>
                <Text strong style={{ color: '#722ed1' }}>{player.gemCount.toLocaleString()}</Text>
              </Space>
            </Card>
          )}

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
            message={`完了: ${result.balanceBefore.toLocaleString()} → ${result.balanceAfter.toLocaleString()} GEM`}
          />
        )}
        {error && (
          <Alert style={{ marginTop: 16 }} type="error" message={error} />
        )}
      </Card>
    </>
  )
}
