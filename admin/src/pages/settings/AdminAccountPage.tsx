import { useEffect, useState } from 'react'
import {
  Table, Tag, Button, Modal, Form, Input, Select, Popconfirm,
  Typography, Alert, Spin, Space,
} from 'antd'
import { PlusOutlined, StopOutlined } from '@ant-design/icons'
import { accountApi } from '../../api/admin'
import type { AdminAccount } from '../../api/types'
import { useAuthStore } from '../../store/authStore'
import dayjs from 'dayjs'

const { Title } = Typography

const ROLE_COLOR: Record<string, string> = {
  super_admin: 'red',
  operator:    'blue',
  viewer:      'default',
}

export default function AdminAccountPage() {
  const isSuperAdmin = useAuthStore((s: { isSuperAdmin: () => boolean }) => s.isSuperAdmin())
  const [accounts, setAccounts] = useState<AdminAccount[]>([])
  const [loading, setLoading]   = useState(true)
  const [error, setError]       = useState<string | null>(null)
  const [open, setOpen]         = useState(false)
  const [saving, setSaving]     = useState(false)
  const [form] = Form.useForm()

  const load = () => {
    setLoading(true)
    accountApi.list()
      .then(setAccounts)
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }
  useEffect(load, [])

  const handleAdd = async (values: { email: string; role: string }) => {
    setSaving(true)
    try {
      await accountApi.upsert(values.email, values.role)
      setOpen(false)
      form.resetFields()
      load()
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setSaving(false)
    }
  }

  const handleDisable = async (email: string) => {
    await accountApi.disable(email)
    load()
  }

  const columns = [
    { title: '管理者番号', dataIndex: 'adminNo', width: 110 },
    { title: 'Google アカウント (email)', dataIndex: 'email' },
    {
      title: 'ロール', dataIndex: 'role',
      render: (r: string) => <Tag color={ROLE_COLOR[r]}>{r}</Tag>,
    },
    {
      title: '状態', dataIndex: 'isActive',
      render: (v: boolean) => v
        ? <Tag color="green">有効</Tag>
        : <Tag color="red">無効</Tag>,
    },
    {
      title: '登録日',
      dataIndex: 'createdAt',
      render: (v: string) => dayjs(v).format('YYYY-MM-DD'),
    },
    {
      title: '',
      render: (_: unknown, r: AdminAccount) => (
        r.isActive && isSuperAdmin ? (
          <Popconfirm
            title={`${r.email} を無効化しますか？`}
            onConfirm={() => handleDisable(r.email)}
            okText="無効化" cancelText="キャンセル" okButtonProps={{ danger: true }}
          >
            <Button size="small" danger icon={<StopOutlined />}>無効化</Button>
          </Popconfirm>
        ) : null
      ),
    },
  ]

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />
  if (!isSuperAdmin) return <Alert type="error" message="Super Admin のみアクセスできます" />

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Title level={4} style={{ margin: 0 }}>管理者アカウント</Title>
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setOpen(true)}>
          追加
        </Button>
      </Space>

      {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} />}

      <Table
        rowKey="adminNo"
        dataSource={accounts}
        columns={columns}
        size="small"
        pagination={false}
      />

      <Modal
        title="管理者アカウント追加"
        open={open}
        onOk={() => form.submit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        okText="追加" cancelText="キャンセル"
      >
        <Form form={form} layout="vertical" onFinish={handleAdd}>
          <Form.Item
            label="Google アカウント (email)"
            name="email"
            rules={[{ required: true, type: 'email' }]}
          >
            <Input placeholder="admin@example.com" />
          </Form.Item>
          <Form.Item label="ロール" name="role" rules={[{ required: true }]} initialValue="operator">
            <Select
              options={[
                { value: 'super_admin', label: 'Super Admin (全権限)' },
                { value: 'operator',   label: 'Operator (一般管理)' },
                { value: 'viewer',     label: 'Viewer (閲覧のみ)' },
              ]}
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
