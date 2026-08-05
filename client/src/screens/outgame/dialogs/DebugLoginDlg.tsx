/**
 * CMJDebugLogin 相当 — 開発環境専用ログインダイアログ (AP-09 §3-5-1)
 *
 * Legacy sources:
 * - legacy/client/HgMajak2/MJDebugLogin.h/cpp
 * - legacy/client/HgMajak2/HgMajak2.rc: IDD_DIALOG_LOGIN
 *
 * IDD_DIALOG_LOGIN: DIALOG 0,0,139,149 CAPTION "LOGIN"
 * Controls:
 * - IDC_COMBO_SERVER 30,15,100,80
 * - IDC_COMBO_GROUP  30,35,100,80
 * - IDC_COMBO_ID     30,60,100,70
 * - IDC_EDIT_PW      30,80,100,14 ES_PASSWORD
 * - IDC_SECURE       30,103,43,10 BS_AUTOCHECKBOX
 * - IDOK             17,125,50,14
 * - IDCANCEL         71,125,50,14
 */
import { useEffect, useState } from 'react'

const DLG_BG = '#d4d0c8'
const BORDER = '#808080'
const LIGHT = '#fff'

const SX = (value: number) => Math.round(value * 1.5)
const SY = (value: number) => Math.round(value * 1.625)

export interface DebugLoginServerOption {
  label: string
  serverId: string
  downloadUrl: string
  passwordServer?: 'real' | 'test' | 'dev'
}

export interface DebugLoginGroupOption {
  label: string
  groupId: string
}

export interface DebugLoginUserOption {
  id: string
  password?: string
}

export interface DebugLoginResult {
  server: DebugLoginServerOption
  group: DebugLoginGroupOption
  userId: string
  password: string
  secure: boolean
  serverIndex: number
  groupIndex: number
  userIndex: number
  loginUri: string
}

interface Props {
  servers: DebugLoginServerOption[]
  groups: DebugLoginGroupOption[]
  users: DebugLoginUserOption[]
  onOK: (result: DebugLoginResult) => void
  onCancel: () => void
}

function buildLoginUri(result: Omit<DebugLoginResult, 'loginUri'>): string {
  const { server, group, userId, password, secure, serverIndex } = result
  const downloadUrl = server.downloadUrl
  const passwordParam = secure ? '&k111e:1=0&k300e:1=0' : `&k111e:${password.length}=${password}`

  return `hangame://majak2channel://metp://${server.serverId}//go/go`
    + ';k7e:41=122NNNN-4_P_V_U_L041_H227_F01_PA4_1I7_458'
    + '&k13e:7=1000000&k31e:4=1360'
    + '&k22e:6=MAJAK2'
    + '&k37e:6=999999'
    + '&k87e:3=hgc'
    + '&k93e:7=Hangame'
    + '&k126e:6=urlhgc'
    + `&k89e:${downloadUrl.length + 5}=${downloadUrl}/dist`
    + `&k88e:${downloadUrl.length + 6}=${downloadUrl}/sdurl`
    + `&k96e:${downloadUrl.length + 4}=${downloadUrl}/hul`
    + '&k95e:25=http://www.hangame.co.jp/'
    + '&k60e:6=安全心'
    + '&nors:1=Y'
    + '&k90e:4=2.06'
    + '&k91e:4=1.76'
    + '&k92e:2=11'
    + '&scvr:1=9'
    + '&k98e:1=0'
    + '&fcvr:0='
    + '&fciv:0='
    + '&k125e:2=22'
    + '&lang:8=JAPANESE'
    + `&k140e:${group.groupId.length}=${group.groupId}`
    + '&k10e:2=31'
    + '&k11e:1=M'
    + '&k8e:4=名前'
    + '&k32e:4=地域'
    + '&k33e:1=7'
    + `&k3e:${userId.length}=${userId}`
    + '&mjkk36e:4=9999'
    + '&kUpGameId:6=majak3'
    + '&k150e:4=2.06'
    + `&p189:1=${serverIndex}`
    + passwordParam
}

function FieldLabel({ x, y, children }: { x: number; y: number; children: string }) {
  return (
    <span style={{
      position: 'absolute', left: SX(x), top: SY(y), width: SX(20), height: SY(8),
      fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))', lineHeight: `${SY(8)}px`, color: '#000',
    }}>
      {children}
    </span>
  )
}

export default function DebugLoginDlg({ servers, groups, users, onOK, onCancel }: Props) {
  const [serverIndex, setServerIndex] = useState(0)
  const [groupIndex, setGroupIndex] = useState(0)
  const [userIndex, setUserIndex] = useState(0)
  const [serverText, setServerText] = useState(servers[0]?.label ?? '')
  const [groupText, setGroupText] = useState(groups[0]?.label ?? '')
  const [userId, setUserId] = useState(users[0]?.id ?? '')
  const [password, setPassword] = useState(users[0]?.password ?? '')
  const [secure, setSecure] = useState(false)

  useEffect(() => {
    const user = users[userIndex]
    if (!user) return
    setUserId(user.id)
    setPassword(user.password ?? '')
  }, [userIndex, users])

  const handleOK = () => {
    const server = servers[serverIndex] ?? { label: serverText, serverId: serverText, downloadUrl: '' }
    const group = groups[groupIndex] ?? { label: groupText, groupId: groupText }
    if (!server || !group || !userId) return

    const resultWithoutUri = {
      server, group, userId, password, secure,
      serverIndex: serverIndex >= 0 ? serverIndex : 0,
      groupIndex,
      userIndex,
    }
    onOK({
      ...resultWithoutUri,
      loginUri: buildLoginUri(resultWithoutUri),
    })
  }

  const comboStyle: React.CSSProperties = {
    position: 'absolute', width: SX(100), height: SY(20),
    fontFamily: 'var(--majak-font-family-ui)', fontSize: 'calc(12px * var(--majak-type-scale))',
  }

  return (
    <div style={{
      position: 'absolute', inset: 0,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'transparent', zIndex: 500,
    }}>
      <div style={{
        position: 'relative', width: SX(139), height: SY(149) + 22,
        background: DLG_BG, borderTop: `1px solid ${LIGHT}`, borderLeft: `1px solid ${LIGHT}`,
        borderRight: `1px solid ${BORDER}`, borderBottom: `1px solid ${BORDER}`,
        boxShadow: '2px 2px 0 rgba(0,0,0,0.35)',
        fontFamily: 'var(--majak-font-family-ui)', color: '#000',
      }}>
        <div style={{
          position: 'absolute', left: 3, top: 3, right: 3, height: 18,
          background: 'linear-gradient(90deg, #000080, #1084d0)', color: '#fff',
          fontSize: 'calc(12px * var(--majak-type-scale))', lineHeight: '18px', paddingLeft: 5,
        }}>
          LOGIN
        </div>
        <div style={{ position: 'absolute', left: 0, top: 22, width: SX(139), height: SY(149) }}>
          <FieldLabel x={5} y={15}>Server</FieldLabel>
          <input
            list="debug-login-server-options"
            value={serverText}
            onChange={event => {
              const value = event.target.value
              const nextIndex = servers.findIndex(server => server.label === value)
              setServerText(value)
              setServerIndex(nextIndex)
            }}
            style={{ ...comboStyle, left: SX(30), top: SY(15) }}
          />
          <datalist id="debug-login-server-options">
            {servers.map((server, index) => <option key={`${server.serverId}-${index}`} value={server.label} />)}
          </datalist>

          <FieldLabel x={5} y={35}>Group</FieldLabel>
          <input
            list="debug-login-group-options"
            value={groupText}
            onChange={event => {
              const value = event.target.value
              const nextIndex = groups.findIndex(group => group.label === value)
              setGroupText(value)
              setGroupIndex(nextIndex)
            }}
            style={{ ...comboStyle, left: SX(30), top: SY(35) }}
          />
          <datalist id="debug-login-group-options">
            {groups.map((group, index) => <option key={`${group.groupId}-${index}`} value={group.label} />)}
          </datalist>

          <FieldLabel x={5} y={65}>ID</FieldLabel>
          <input
            list="debug-login-user-options"
            value={userId}
            onChange={event => {
              const value = event.target.value
              const nextIndex = users.findIndex(user => user.id === value)
              setUserIndex(nextIndex)
              setUserId(value)
              if (nextIndex >= 0) setPassword(users[nextIndex]?.password ?? '')
            }}
            style={{ ...comboStyle, left: SX(30), top: SY(60) }}
          />
          <datalist id="debug-login-user-options">
            {users.map((user, index) => <option key={`${user.id}-${index}`} value={user.id} />)}
          </datalist>

          <FieldLabel x={5} y={85}>PW</FieldLabel>
          <input
            type="password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            style={{ ...comboStyle, left: SX(30), top: SY(80), height: SY(14) }}
          />

          <label style={{
            position: 'absolute', left: SX(30), top: SY(103), width: SX(80), height: SY(10),
            display: 'flex', alignItems: 'center', gap: 4, fontSize: 'calc(12px * var(--majak-type-scale))',
          }}>
            <input type="checkbox" checked={secure} onChange={event => setSecure(event.target.checked)} style={{ margin: 0 }} />
            クロ保護
          </label>

          <button onClick={handleOK} style={{ position: 'absolute', left: SX(17), top: SY(125), width: SX(50), height: SY(14), fontSize: 'calc(12px * var(--majak-type-scale))', padding: 0 }}>
            OK
          </button>
          <button onClick={onCancel} style={{ position: 'absolute', left: SX(71), top: SY(125), width: SX(50), height: SY(14), fontSize: 'calc(12px * var(--majak-type-scale))', padding: 0 }}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  )
}