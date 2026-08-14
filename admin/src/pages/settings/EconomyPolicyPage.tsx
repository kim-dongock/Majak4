import { useEffect, useState } from 'react'
import { Alert, Button, Card, Form, InputNumber, Spin, Typography, message } from 'antd'
import { SaveOutlined } from '@ant-design/icons'
import { economyPolicyApi } from '../../api/admin'
import { useAuthStore } from '../../store/authStore'

const { Title, Paragraph, Text } = Typography

export default function EconomyPolicyPage() {
  const isSuperAdmin = useAuthStore((state) => state.isSuperAdmin())
  const [form] = Form.useForm()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [updatedAt, setUpdatedAt] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const policy = await economyPolicyApi.get()
      form.setFieldsValue(policy)
      setUpdatedAt(policy.updatedAt)
    } catch (requestError) {
      setError((requestError as Error).message)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])

  const save = async () => {
    const values = await form.validateFields()
    setSaving(true)
    setError(null)
    try {
      const policy = await economyPolicyApi.update(values)
      form.setFieldsValue(policy)
      setUpdatedAt(policy.updatedAt)
      message.success('GP経済設定を保存しました。次の対象処理から反映されます。')
    } catch (requestError) {
      setError((requestError as Error).message)
    } finally {
      setSaving(false)
    }
  }

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />

  return (
    <>
      <Title level={4}>GP経済設定</Title>
      <Paragraph type="secondary">
        新規登録時のGPと無料GP補充を管理します。場代は公式卓設定のまま変更されません。
      </Paragraph>
      {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}
      {!isSuperAdmin && <Alert type="info" message="閲覧専用 — 編集は Super Admin のみ可能" style={{ marginBottom: 16 }} />}
      <Card style={{ maxWidth: 560 }}>
        <Form form={form} layout="vertical" disabled={!isSuperAdmin}>
          <Form.Item label="新規登録時のGP" name="initialGp" rules={[{ required: true, type: 'number', min: 0 }]}>
            <InputNumber min={0} precision={0} addonAfter="GP" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="無料補充後のGP" name="freeReplenishTargetGp" rules={[{ required: true, type: 'number', min: 0 }]}>
            <InputNumber min={0} precision={0} addonAfter="GP" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="1日あたりの無料補充回数" name="freeReplenishDailyLimit" rules={[{ required: true, type: 'number', min: 0, max: 100 }]}>
            <InputNumber min={0} max={100} precision={0} addonAfter="回" style={{ width: '100%' }} />
          </Form.Item>
          {updatedAt && <Text type="secondary">最終更新: {new Date(updatedAt).toLocaleString()}</Text>}
          <div style={{ marginTop: 20 }}>
            <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void save()}>
              保存
            </Button>
          </div>
        </Form>
      </Card>
    </>
  )
}