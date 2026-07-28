import { useState } from 'react'
import {
  Card, Form, Input, Select, Button, Alert, Typography, Divider,
} from 'antd'
import { noticeApi } from '../../api/admin'

const { Title, Text } = Typography

const COLOR_OPTIONS = [
  { value: 0, label: '白 (通常)' },
  { value: 1, label: '赤 (重要)' },
  { value: 2, label: '黄 (警告)' },
  { value: 3, label: '青 (情報)' },
]

export default function NoticePage() {
  const [form] = Form.useForm()
  const [sending, setSending] = useState(false)
  const [result, setResult]   = useState<'ok' | null>(null)
  const [error, setError]     = useState<string | null>(null)

  const handleSend = async (values: { message: string; color: number }) => {
    setSending(true)
    setResult(null)
    setError(null)
    try {
      await noticeApi.send(values.message, values.color)
      setResult('ok')
      form.resetFields()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSending(false)
    }
  }

  return (
    <>
      <Title level={4}>公知送信</Title>
      <Card style={{ maxWidth: 560 }}>
        <Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
          現在接続中の全プレイヤーにシステムメッセージを送信します。
        </Text>
        <Divider />
        <Form
          form={form}
          layout="vertical"
          initialValues={{ color: 0 }}
          onFinish={handleSend}
        >
          <Form.Item
            label="メッセージ"
            name="message"
            rules={[{ required: true, min: 1, max: 200 }]}
          >
            <Input.TextArea rows={4} maxLength={200} showCount placeholder="公知内容を入力..." />
          </Form.Item>
          <Form.Item label="文字色" name="color">
            <Select options={COLOR_OPTIONS} style={{ width: 160 }} />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={sending}>
            全員に送信
          </Button>
        </Form>
        {result === 'ok' && (
          <Alert style={{ marginTop: 16 }} type="success" message="公知を送信しました" />
        )}
        {error && (
          <Alert style={{ marginTop: 16 }} type="error" message={error} />
        )}
      </Card>
    </>
  )
}
