import { useEffect, useState } from 'react'
import {
  Alert, Button, Form, Input, InputNumber, Modal, Spin, Switch, Table, Tag, Typography,
} from 'antd'
import { EditOutlined, ReloadOutlined } from '@ant-design/icons'
import { channelApi } from '../../api/admin'
import type { ChannelMaster } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title } = Typography

export default function ChannelListPage() {
  const isSuperAdmin = useAuthStore((state: { isSuperAdmin: () => boolean }) => state.isSuperAdmin())
  const [channels, setChannels] = useState<ChannelMaster[]>([])
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<ChannelMaster | null>(null)
  const [form] = Form.useForm()

  const load = () => {
    setLoading(true)
    setError(null)
    channelApi.list()
      .then(setChannels)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false))
  }

  useEffect(load, [])

  const openEdit = (channel: ChannelMaster) => {
    setEditing(channel)
    form.setFieldsValue(channel)
  }

  const handleSave = async () => {
    if (!editing) return
    const values = await form.validateFields()
    setSaving(true)
    setError(null)
    try {
      await channelApi.update({ ...editing, ...values })
      setEditing(null)
      load()
    } catch (reason) {
      setError((reason as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const columns = [
    { title: 'チャンネル ID', dataIndex: 'channelId', width: 180 },
    { title: 'Sub ID', dataIndex: 'subId', width: 90 },
    { title: 'チャンネル名', dataIndex: 'channelName' },
    { title: '最大ルーム数', dataIndex: 'maxRoom', width: 120 },
    { title: 'サーバー URL', dataIndex: 'serverUrl', render: (value: string) => value || '未設定' },
    {
      title: 'サーバー状態', dataIndex: 'serverActive', width: 110,
      render: (active: boolean) => <Tag color={active ? 'green' : 'red'}>{active ? '稼働中' : '停止中'}</Tag>,
    },
    {
      title: '公開', dataIndex: 'isActive', width: 80,
      render: (active: boolean) => <Tag color={active ? 'blue' : 'default'}>{active ? '有効' : '無効'}</Tag>,
    },
    {
      title: '', width: 72,
      render: (_: unknown, channel: ChannelMaster) => isSuperAdmin ? (
        <Button
          type="text"
          icon={<EditOutlined />}
          aria-label={`${channel.channelName}を編集`}
          title="編集"
          onClick={() => openEdit(channel)}
        />
      ) : null,
    },
  ]

  if (loading && channels.length === 0)
    return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />

  return (
    <>
      <Title level={4}>チャンネル管理</Title>
      {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}
      {!isSuperAdmin && (
        <Alert type="info" message="閲覧専用 — 編集は Super Admin のみ可能" style={{ marginBottom: 16 }} />
      )}
      <Button
        icon={<ReloadOutlined />}
        loading={loading}
        onClick={load}
        style={{ marginBottom: 12 }}
      >
        状態更新
      </Button>
      <Table
        rowKey="channelId"
        dataSource={channels}
        columns={columns}
        size="small"
        pagination={false}
        scroll={{ x: 1000 }}
      />

      <Modal
        title="チャンネル編集"
        open={!!editing}
        onOk={handleSave}
        onCancel={() => setEditing(null)}
        confirmLoading={saving}
        okText="保存"
        cancelText="キャンセル"
      >
        <Form form={form} layout="vertical">
          <Form.Item label="チャンネル名" name="channelName" rules={[{ required: true }]}>
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item
            label="サーバー URL"
            name="serverUrl"
            dependencies={['isActive']}
            rules={[
              ({ getFieldValue }) => ({
                validator: (_, value: string) => {
                  if (!getFieldValue('isActive') && !value) return Promise.resolve()
                  try {
                    const url = new URL(value)
                    return ['http:', 'https:'].includes(url.protocol) ? Promise.resolve() : Promise.reject(new Error())
                  } catch {
                    return Promise.reject(new Error('HTTP(S) のサーバー URL を入力してください'))
                  }
                },
              }),
            ]}
          >
            <Input placeholder="https://game-server.example.com" />
          </Form.Item>
          <Form.Item label="公開" name="isActive" valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item label="最大人数" name="maxMember" rules={[{ required: true, type: 'number', min: 1 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="最大ルーム数" name="maxRoom" rules={[{ required: true, type: 'number', min: 1 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="単位 GP" name="unitMoney" rules={[{ required: true, type: 'number', min: 0 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="チャンネルタイプ" name="channelType" rules={[{ required: true, type: 'number', min: 0, max: 255 }]}>
            <InputNumber style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
