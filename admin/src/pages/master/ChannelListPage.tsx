import { Typography, Alert } from 'antd'

const { Title } = Typography

export default function ChannelListPage() {
  return (
    <>
      <Title level={4}>チャンネル一覧</Title>
      <Alert
        type="info"
        message="チャンネル情報は /api/channels から取得できます。実装予定。"
      />
    </>
  )
}
