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

  return (
    <div className="majak-popup-overlay">
      <section className="majak-popup-panel majak-debug-login-dialog" role="dialog" aria-modal="true" aria-labelledby="debug-login-dialog-title">
        <header id="debug-login-dialog-title" className="majak-popup-titlebar"><span>ログイン</span><button className="majak-popup-titlebar__close" type="button" onClick={onCancel} aria-label="閉じる">×</button></header>
        <div className="majak-popup-body majak-debug-login-dialog__body">
          <label>
            <span>Server</span>
          <input
            list="debug-login-server-options"
            value={serverText}
            onChange={event => {
              const value = event.target.value
              const nextIndex = servers.findIndex(server => server.label === value)
              setServerText(value)
              setServerIndex(nextIndex)
            }}
          />
          </label>
          <datalist id="debug-login-server-options">
            {servers.map((server, index) => <option key={`${server.serverId}-${index}`} value={server.label} />)}
          </datalist>

          <label>
            <span>Group</span>
          <input
            list="debug-login-group-options"
            value={groupText}
            onChange={event => {
              const value = event.target.value
              const nextIndex = groups.findIndex(group => group.label === value)
              setGroupText(value)
              setGroupIndex(nextIndex)
            }}
          />
          </label>
          <datalist id="debug-login-group-options">
            {groups.map((group, index) => <option key={`${group.groupId}-${index}`} value={group.label} />)}
          </datalist>

          <label>
            <span>ID</span>
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
          />
          </label>
          <datalist id="debug-login-user-options">
            {users.map((user, index) => <option key={`${user.id}-${index}`} value={user.id} />)}
          </datalist>

          <label>
            <span>PW</span>
          <input
            type="password"
            value={password}
            onChange={event => setPassword(event.target.value)}
          />
          </label>

          <label className="majak-debug-login-dialog__secure">
            <input type="checkbox" checked={secure} onChange={event => setSecure(event.target.checked)} />
            クロ保護
          </label>
        </div>
        <footer className="majak-popup-actions">
          <button type="button" onClick={onCancel}>キャンセル</button>
          <button type="button" className="is-primary" onClick={handleOK}>OK</button>
        </footer>
      </section>
    </div>
  )
}