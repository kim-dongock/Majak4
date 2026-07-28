import { Card, Typography, message, Space } from 'antd'
import { GoogleLogin } from '@react-oauth/google'
import { useNavigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { authApi } from '../api/admin'

const { Title, Text } = Typography

export default function LoginPage() {
  const navigate = useNavigate()
  const setAuth = useAuthStore((s: { setAuth: (t: string, e: string, r: string) => void }) => s.setAuth)
  const [messageApi, contextHolder] = message.useMessage()

  return (
    <div style={{
      height: '100vh', display: 'flex',
      alignItems: 'center', justifyContent: 'center',
      background: 'linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)',
    }}>
      {contextHolder}
      <Card style={{ width: 400, textAlign: 'center', borderRadius: 12 }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div>
            <div style={{ fontSize: 48, marginBottom: 8 }}>🀄</div>
            <Title level={3} style={{ margin: 0 }}>Majak4 Admin</Title>
            <Text type="secondary">管理者専用サイト</Text>
          </div>

          <div style={{ display: 'flex', justifyContent: 'center' }}>
            <GoogleLogin
              onSuccess={async (credentialResponse) => {
                if (!credentialResponse.credential) return
                try {
                  const result = await authApi.loginWithGoogle(credentialResponse.credential)
                  setAuth(result.token, result.email, result.role)
                  navigate('/dashboard')
                } catch {
                  messageApi.error('ログインに失敗しました。アカウントが許可されていない可能性があります')
                }
              }}
              onError={() => messageApi.error('Google ログインに失敗しました')}
              size="large"
              text="signin_with"
              shape="rectangular"
              logo_alignment="left"
            />
          </div>

          <Text type="secondary" style={{ fontSize: 12 }}>
            許可された Google アカウントのみログインできます
          </Text>
        </Space>
      </Card>
    </div>
  )
}
