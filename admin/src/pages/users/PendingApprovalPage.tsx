import { useState } from 'react'
import { Table, Button, Space, Tag, Modal, Input, message, Badge } from 'antd'
import { CheckCircleOutlined, StopOutlined, ReloadOutlined } from '@ant-design/icons'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { userApi } from '../../api/admin'
import type { PendingPlayer } from '../../api/types'

export default function PendingApprovalPage() {
  const [suspendTarget, setSuspendTarget] = useState<PendingPlayer | null>(null)
  const [suspendReason, setSuspendReason] = useState('')
  const queryClient = useQueryClient()

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['pending-players'],
    queryFn: () => userApi.getPending(0, 100),
  })

  const approveMut = useMutation({
    mutationFn: (memberNo: number) => userApi.approve(memberNo),
    onSuccess: () => {
      message.success('承認しました')
      queryClient.invalidateQueries({ queryKey: ['pending-players'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard'] })
    },
    onError: () => message.error('承認に失敗しました'),
  })

  const suspendMut = useMutation({
    mutationFn: ({ memberNo, reason }: { memberNo: number; reason: string }) =>
      userApi.suspend(memberNo, reason),
    onSuccess: () => {
      message.success('却下しました')
      setSuspendTarget(null)
      setSuspendReason('')
      queryClient.invalidateQueries({ queryKey: ['pending-players'] })
    },
    onError: () => message.error('却下に失敗しました'),
  })

  const items = data?.items ?? []
  const total = data?.total ?? 0

  const columns = [
    {
      title: 'ニックネーム',
      dataIndex: 'displayName',
      key: 'displayName',
    },
    {
      title: 'アバター',
      dataIndex: 'avatarId',
      key: 'avatarId',
      render: (v: string) => v || '—',
    },
    {
      title: '会員番号',
      dataIndex: 'memberNo',
      key: 'memberNo',
      width: 90,
    },
    {
      title: '性別',
      dataIndex: 'sexCode',
      key: 'sexCode',
      width: 60,
      render: (v: string) => (
        <Tag color={v === 'M' ? 'blue' : v === 'F' ? 'pink' : 'default'}>
          {v === 'M' ? '男' : v === 'F' ? '女' : '不明'}
        </Tag>
      ),
    },
    {
      title: '規約同意日時',
      dataIndex: 'termsAgreedAt',
      key: 'termsAgreedAt',
      render: (v: string) => new Date(v).toLocaleString('ja-JP'),
    },
    {
      title: '登録日時',
      dataIndex: 'registeredAt',
      key: 'registeredAt',
      render: (v: string) => new Date(v).toLocaleString('ja-JP'),
    },
    {
      title: '操作',
      key: 'action',
      width: 180,
      render: (_: unknown, record: PendingPlayer) => (
        <Space>
          <Button
            type="primary"
            size="small"
            icon={<CheckCircleOutlined />}
            loading={approveMut.isPending && approveMut.variables === record.memberNo}
            onClick={() => approveMut.mutate(record.memberNo)}
          >
            承認
          </Button>
          <Button
            danger
            size="small"
            icon={<StopOutlined />}
            onClick={() => { setSuspendTarget(record); setSuspendReason('') }}
          >
            却下
          </Button>
        </Space>
      ),
    },
  ]

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h2 style={{ margin: 0 }}>
          承認待ちプレイヤー{' '}
          <Badge count={total} overflowCount={999} style={{ backgroundColor: '#ff4d4f' }} />
        </h2>
        <Button icon={<ReloadOutlined />} onClick={() => refetch()}>更新</Button>
      </div>

      <Table<PendingPlayer>
        dataSource={items}
        columns={columns}
        rowKey="memberNo"
        loading={isLoading}
        pagination={false}
        locale={{ emptyText: '承認待ちのプレイヤーはいません' }}
      />

      {/* 却下理由モーダル */}
      <Modal
        title="却下理由を入力"
        open={suspendTarget !== null}
        onOk={() => {
          if (!suspendTarget) return
          suspendMut.mutate({ memberNo: suspendTarget.memberNo, reason: suspendReason })
        }}
        onCancel={() => { setSuspendTarget(null); setSuspendReason('') }}
        okText="却下する"
        okButtonProps={{ danger: true, loading: suspendMut.isPending }}
        cancelText="キャンセル"
      >
        <p>ニックネーム: <strong>{suspendTarget?.displayName}</strong></p>
        <Input.TextArea
          rows={3}
          placeholder="却下理由 (任意)"
          value={suspendReason}
          onChange={e => setSuspendReason(e.target.value)}
        />
      </Modal>
    </div>
  )
}
