import { useEffect, useState } from 'react'
import {
  Table, Tag, Switch, Button, Modal, Form, Input, InputNumber,
  Typography, Alert, Spin,
} from 'antd'
import { EditOutlined } from '@ant-design/icons'
import { gemApi } from '../../api/admin'
import type { GemProduct } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title } = Typography

const PLATFORM_COLOR: Record<string, string> = {
  web: 'blue', ios: 'green', android: 'orange', all: 'default',
}

export default function GemProductPage() {
  const isSuperAdmin = useAuthStore((s: { isSuperAdmin: () => boolean }) => s.isSuperAdmin())
  const [products, setProducts] = useState<GemProduct[]>([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState<string | null>(null)
  const [editing, setEditing]   = useState<GemProduct | null>(null)
  const [saving, setSaving]     = useState(false)
  const [form] = Form.useForm()

  const load = () => {
    setLoading(true)
    gemApi.getProducts()
      .then(setProducts)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }
  useEffect(load, [])

  const openEdit = (p: GemProduct) => {
    setEditing(p)
    form.setFieldsValue(p)
  }

  const handleSave = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      await gemApi.updateProduct({ ...editing!, ...values })
      setEditing(null)
      load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const columns = [
    {
      title: 'プラットフォーム', dataIndex: 'platform',
      render: (p: string) => <Tag color={PLATFORM_COLOR[p]}>{p}</Tag>,
    },
    { title: '商品 ID', dataIndex: 'productId' },
    { title: '表示名', dataIndex: 'displayName' },
    { title: 'GEM', dataIndex: 'gemAmount', render: (v: number) => `${v.toLocaleString()} GEM` },
    { title: '価格', dataIndex: 'priceJpy', render: (v: number) => `¥${v.toLocaleString()}` },
    { title: 'ストア商品 ID', dataIndex: 'storeProductId', render: (v: string | null) => v ?? '—' },
    {
      title: '有効', dataIndex: 'isActive',
      render: (v: boolean, r: GemProduct) => (
        <Switch
          checked={v}
          disabled={!isSuperAdmin}
          onChange={(checked) => {
            gemApi.updateProduct({ ...r, isActive: checked }).then(load)
          }}
        />
      ),
    },
    {
      title: '',
      render: (_: unknown, r: GemProduct) =>
        isSuperAdmin ? (
          <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(r)}>編集</Button>
        ) : null,
    },
  ]

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />

  return (
    <>
      <Title level={4}>GEM 商品マスター</Title>
      {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}
      {!isSuperAdmin && (
        <Alert type="info" message="閲覧専用 — 編集は Super Admin のみ可能" style={{ marginBottom: 16 }} />
      )}

      <Table
        rowKey="productId"
        dataSource={products}
        columns={columns}
        size="small"
        pagination={false}
      />

      <Modal
        title="商品編集"
        open={!!editing}
        onOk={handleSave}
        onCancel={() => setEditing(null)}
        confirmLoading={saving}
        okText="保存"
        cancelText="キャンセル"
      >
        <Form form={form} layout="vertical">
          <Form.Item label="表示名" name="displayName" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item label="GEM 数量" name="gemAmount" rules={[{ required: true, type: 'number', min: 1 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="価格 (円)" name="priceJpy" rules={[{ required: true, type: 'number', min: 1 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="ストア商品 ID" name="storeProductId">
            <Input placeholder="例: jp.hange.majak2.gem100" />
          </Form.Item>
          <Form.Item label="表示順" name="sortOrder" rules={[{ required: true, type: 'number', min: 0 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
