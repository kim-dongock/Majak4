import { useEffect, useState } from 'react'
import { Alert, Button, Form, Image, Input, InputNumber, Modal, Spin, Switch, Table, Tag, Typography } from 'antd'
import { EditOutlined } from '@ant-design/icons'
import { convenienceItemApi } from '../../api/admin'
import type { ConvenienceItem } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title } = Typography

export default function ConvenienceItemPage() {
  const isSuperAdmin = useAuthStore((state: { isSuperAdmin: () => boolean }) => state.isSuperAdmin())
  const [items, setItems] = useState<ConvenienceItem[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<ConvenienceItem | null>(null)
  const [form] = Form.useForm<ConvenienceItem>()

  const load = () => {
    setLoading(true)
    convenienceItemApi.list().then(setItems).catch((reason: Error) => setError(reason.message)).finally(() => setLoading(false))
  }
  useEffect(load, [])

  const openEdit = (item: ConvenienceItem) => {
    setEditing(item)
    form.setFieldsValue(item)
  }

  const save = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      await convenienceItemApi.update({ ...editing!, ...values })
      setEditing(null)
      load()
    } catch (reason) {
      setError((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const columns = [
    { title: '商品コード', render: (_: unknown, item: ConvenienceItem) => `${item.itemCode} / ${item.sellCode}` },
    { title: '画像', dataIndex: 'imageUrl', render: (url: string) => url ? <Image width={52} height={52} preview src={url} style={{ objectFit: 'contain' }} /> : '-' },
    { title: '表示名', dataIndex: 'itemName' },
    { title: 'MP価格', dataIndex: 'cashPrice', render: (price: number) => `${price.toLocaleString()} MP` },
    { title: '説明', dataIndex: 'description', ellipsis: true },
    { title: '順序', dataIndex: 'sortOrder' },
    { title: '販売', dataIndex: 'isOnSale', render: (value: boolean) => value ? <Tag color="green">販売中</Tag> : <Tag>停止</Tag> },
    { title: '', render: (_: unknown, item: ConvenienceItem) => isSuperAdmin ? <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(item)}>編集</Button> : null },
  ]

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />

  return <>
    <Title level={4}>便利アイテムマスター</Title>
    {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}
    {!isSuperAdmin && <Alert type="info" message="閲覧専用 — 編集は Super Admin のみ可能" style={{ marginBottom: 16 }} />}
    <Table rowKey={item => `${item.itemCode}-${item.sellCode}`} dataSource={items} columns={columns} size="small" pagination={false} />
    <Modal title="便利アイテム編集" open={!!editing} onOk={save} onCancel={() => setEditing(null)} confirmLoading={saving} okText="保存" cancelText="キャンセル">
      <Form form={form} layout="vertical">
        <Form.Item label="表示名" name="itemName" rules={[{ required: true, max: 30 }]}><Input maxLength={30} /></Form.Item>
        <Form.Item label="MP価格" name="cashPrice" rules={[{ required: true, type: 'number', min: 0 }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item label="説明" name="description" rules={[{ max: 300 }]}><Input.TextArea rows={4} maxLength={300} showCount /></Form.Item>
        <Form.Item label="画像 URL" name="imageUrl" rules={[{ max: 255 }]}><Input placeholder="/assets/images/game/items/...png" maxLength={255} /></Form.Item>
        <Form.Item label="表示順" name="sortOrder" rules={[{ required: true, type: 'number', min: 0 }]}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
        <Form.Item label="販売する" name="isOnSale" valuePropName="checked"><Switch /></Form.Item>
      </Form>
    </Modal>
  </>
}