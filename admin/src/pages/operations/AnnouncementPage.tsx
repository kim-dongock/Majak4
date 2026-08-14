import { useEffect, useState } from 'react'
import { Alert, Button, Card, Checkbox, Form, Input, List, Popconfirm, Space, Spin, Tag, Typography, message } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { announcementApi } from '../../api/admin'
import type { GameAnnouncement } from '../../api/types'
import { useAuthStore } from '../../store/authStore'

const { Title, Text } = Typography
type ArticleInput = Omit<GameAnnouncement, 'announcementId' | 'publishedAt' | 'createdAt' | 'updatedAt'>

export default function AnnouncementPage() {
  const isSuperAdmin = useAuthStore((state) => state.isSuperAdmin())
  const [articles, setArticles] = useState<GameAnnouncement[]>([])
  const [editing, setEditing] = useState<GameAnnouncement | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [form] = Form.useForm<ArticleInput>()

  const load = async () => {
    setLoading(true)
    try { setArticles(await announcementApi.list()) } catch (requestError) { setError((requestError as Error).message) } finally { setLoading(false) }
  }
  useEffect(() => { void load() }, [])

  const beginCreate = () => { setEditing(null); form.setFieldsValue({ title: '', body: '', isPublished: false, isStartup: false }) }
  const beginEdit = (article: GameAnnouncement) => { setEditing(article); form.setFieldsValue(article) }
  const save = async (values: ArticleInput) => {
    setSaving(true); setError(null)
    try {
      if (editing) await announcementApi.update({ ...editing, ...values })
      else await announcementApi.create(values)
      message.success('お知らせを保存しました。')
      beginCreate()
      await load()
    } catch (requestError) { setError((requestError as Error).message) } finally { setSaving(false) }
  }
  const remove = async (article: GameAnnouncement) => {
    try { await announcementApi.remove(article.announcementId); message.success('お知らせを削除しました。'); await load() }
    catch (requestError) { setError((requestError as Error).message) }
  }

  if (loading) return <Spin size="large" style={{ marginTop: 80, display: 'block', textAlign: 'center' }} />
  return <>
    <Title level={4}>お知らせ記事</Title>
    <Text type="secondary">開始時に表示する記事は1件だけ選べます。公開済みの開始記事が、ゲームのタイトル画面とログイン直後のポップアップに表示されます。</Text>
    {error && <Alert type="error" message={error} style={{ margin: '16px 0' }} />}
    {!isSuperAdmin && <Alert type="info" message="閲覧専用 — 編集は Super Admin のみ可能" style={{ margin: '16px 0' }} />}
    <Card title={editing ? 'お知らせを編集' : '新しいお知らせ'} style={{ maxWidth: 760, marginTop: 16 }}>
      <Form form={form} layout="vertical" disabled={!isSuperAdmin} onFinish={save} initialValues={{ isPublished: false, isStartup: false }}>
        <Form.Item name="title" label="タイトル" rules={[{ required: true, max: 120 }]}><Input maxLength={120} /></Form.Item>
        <Form.Item name="body" label="本文" rules={[{ required: true, max: 10000 }]}><Input.TextArea rows={7} maxLength={10000} showCount /></Form.Item>
        <Space size="large"><Form.Item name="isPublished" valuePropName="checked" noStyle><Checkbox>公開する</Checkbox></Form.Item><Form.Item name="isStartup" valuePropName="checked" noStyle><Checkbox>開始時に表示する</Checkbox></Form.Item></Space>
        <div style={{ marginTop: 18 }}><Button type="primary" htmlType="submit" icon={<PlusOutlined />} loading={saving}>{editing ? '保存' : '記事を追加'}</Button>{editing && <Button style={{ marginLeft: 8 }} onClick={beginCreate}>新規作成に戻る</Button>}</div>
      </Form>
    </Card>
    <List style={{ maxWidth: 760, marginTop: 24 }} dataSource={articles} locale={{ emptyText: 'お知らせ記事はありません。' }} renderItem={article => <List.Item actions={isSuperAdmin ? [<Button key="edit" icon={<EditOutlined />} onClick={() => beginEdit(article)}>編集</Button>, <Popconfirm key="delete" title="この記事を削除しますか？" onConfirm={() => void remove(article)}><Button danger icon={<DeleteOutlined />}>削除</Button></Popconfirm>] : []}>
      <List.Item.Meta title={<Space>{article.title}{article.isPublished ? <Tag color="green">公開中</Tag> : <Tag>非公開</Tag>}{article.isStartup && <Tag color="gold">開始時表示</Tag>}</Space>} description={article.body.length > 100 ? `${article.body.slice(0, 100)}...` : article.body} />
    </List.Item>} />
  </>
}