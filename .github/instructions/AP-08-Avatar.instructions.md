---
applyTo: 'client/**,server/**'
description: 'アバター表示フロー・URL生成規約 (重要機能)'
---

# AP-08 アバター表示フロー

## 1. 概要

アバターは Hangame の外部サーバーから GIF 形式で配信される。  
レガシー C++ クライアントの `CHgAniAvatar::getAvatarFileName()` と  
`HgAvatarUtil.cpp::GetAvatarVersion()` を `client/src/utils/resources.ts` で完全再現している。

**⚠️ 使用する関数を用途ごとに必ず使い分けること。混在禁止。**

---

## 2. アバターバージョン判定

| `avatarId` 先頭文字 | バージョン | 備考 |
|---|---|---|
| `N` / `A` / `B` | v1 (旧形式) | アニメーション無し |
| `1` | v2 | 標準 |
| `2` | v3 | 最新形式 |

> 実装: `getAvatarVersion(avatarId)` (resources.ts 内部関数)

---

## 3. URL プレフィックス体系

プレフィックスは **3 文字のコード + `_`** で構成される。

```
[nImageSize][nHalfCut][nAni]_
```

| フィールド | 値 | 意味 |
|---|---|---|
| `nImageSize` | `AW` | AVATAR_GIF_WEB — Web/プロフィール用大サイズ |
| `nImageSize` | `AG` | AVATAR_GIF_GAME — ゲーム用サイズ |
| `nImageSize` | `AC` | AVATAR_GIF_CHANNEL — チャンネル用小サイズ |
| `nHalfCut` | `F` | AVATAR_GIF_BUST — 全身 (Japanese BUST = Full) |
| `nHalfCut` | `H` | AVATAR_GIF_HALF — 半身 (腰より上のみ) |
| `nAni` | `A` | v2 アバター向けアニメ |
| `nAni` | `S` | v3 アバター向けアニメ (AVATAR_GIF_SUPPORT) |

### 実際のプレフィックス組み合わせ

| 用途 | プレフィックス (v2) | プレフィックス (v3) |
|---|---|---|
| プロフィール / ダイアログ / 自分表示 | `AWFA_` | `AWFS_` |
| ゲーム卓上 | `AGFA_` | `AGBS_` |
| 接続者リスト (짧은/短い) | `ACHA_` | `ACHS_` |

---

## 4. 配信ホスト

| 関数 | ホスト | 用途 |
|---|---|---|
| `getAvatarUrl()` | `http://avatar.hange.jp` | 通常アバター |
| `getShortAvatarUrl()` | `http://alpha-avatar.hange.jp` | 接続者リスト用短縮アバター |

---

## 5. API 関数 (resources.ts)

### 5.1 `getAvatarUrl(avatarId)` — Web 大サイズ

```ts
// 生成例
getAvatarUrl("1121NNA-4_P_3_V_U_L0FU_F1GT")
// → "http://avatar.hange.jp/IMG_AVTR/AWFA_1121NNA-4_P_3_V_U_L0FU_F1GT.GIF"

getAvatarUrl("2XXXXX...")
// → "http://avatar.hange.jp/IMG_AVTR/AWFS_2XXXXX....GIF"
```

### 5.2 `getGameAvatarUrl(avatarId)` — ゲーム卓上サイズ

```ts
// 生成例
getGameAvatarUrl("1121NNA-4_P_3_V_U_L0FU_F1GT")
// → "http://avatar.hange.jp/IMG_AVTR/AGFA_1121NNA-4_P_3_V_U_L0FU_F1GT.GIF"

getGameAvatarUrl("2XXXXX...")
// → "http://avatar.hange.jp/IMG_AVTR/AGBS_2XXXXX....GIF"
```

### 5.3 `getShortAvatarUrl(avatarId)` — 半身・チャンネル用

```ts
// 生成例
getShortAvatarUrl("121NNNN-4_P_M_F01_P01_84JN_M8W7_4CD9")
// → "http://alpha-avatar.hange.jp/IMG_AVTR/ACHS_121NNNN-4_P_M_F01_P01_84JN_M8W7_4CD9.GIF"
```

### 5.4 `getDefaultAvatarUrl(sex)` — フォールバック

```ts
getDefaultAvatarUrl('male')   // → '/assets/images/default_avt_web_m.png'
getDefaultAvatarUrl('female') // → '/assets/images/default_avt_web_f.png'
```

- `avatarId` が `null` / `undefined` / 空文字 のとき全関数が自動でこれを返す
- `<img onError>` でも同 URL に差し替えること

---

## 6. 使用箇所マッピング ⚠️ 重要

| コンポーネント | 対象 | 使用関数 |
|---|---|---|
| `RightSidebar.tsx` | チャンネルメンバーリスト (接続者) | **`getShortAvatarUrl`** |
| `RightSidebar.tsx` | 自分のプロフィールカード (`myAvatarSrc` はコンテナから渡す) | — (親で生成) |
| `ChannelLobby.tsx` | 自分のプロフィール用 `myAvatarSrc` | `getAvatarUrl` |
| `RoomList.tsx` | 自分のプロフィール用 `myAvatarSrc` | `getAvatarUrl` |
| `RoomLobby.tsx` | 入室プレイヤー一覧 | `getAvatarUrl` |
| `MemberInfoDialog.tsx` | メンバー詳細ダイアログ | `getAvatarUrl` |
| `ItemInventoryModal.tsx` | アイテム確認モーダル (自分) | `getAvatarUrl` |

> **原則**: 「小さいリスト行に並ぶ他プレイヤーのアバター」→ `getShortAvatarUrl`  
> 「プロフィールカード・ダイアログ・自分のアバター」→ `getAvatarUrl`

---

## 7. 新規コンポーネントに追加するときのチェックリスト

1. **対象は接続者/メンバーリストの行アバターか？**  
   → Yes: `getShortAvatarUrl(member.avatarId)`  
   → No: `getAvatarUrl(player.avatarKey)`

2. **`onError` フォールバックを必ず追加する**

   接続者・観戦者リストの短縮画像 (`ACHA` / `ACHS`) が存在しない場合は、
   `handleShortAvatarError` で同じアバターIDのWeb半身画像 (`AWHA` / `AWHS`) を再試行し、
   それも失敗した場合だけデフォルト画像へ差し替える。

   ```tsx
   <img
     src={getShortAvatarUrl(m.avatarId)}
     alt=""
       onError={(e) => {
          handleShortAvatarError(e.currentTarget, m.avatarId, 'male')
       }}
   />
   ```

3. **インポートは `@utils/resources` から行う**

   ```ts
   import { getAvatarUrl, getShortAvatarUrl, getDefaultAvatarUrl } from '@utils/resources'
   ```

4. **サーバーから受け取る `avatarId` / `avatarKey` は raw 文字列のまま渡す**  
   (URL エンコード・デコード不要)

---

## 8. レガシー対応表

| レガシー定数 | 値 | Web 再現 |
|---|---|---|
| `AVATAR_GIF_WEB` | `"AW"` | `getAvatarUrl` の `nImageSize` |
| `AVATAR_GIF_GAME` | `"AG"` | `getGameAvatarUrl` の `nImageSize` |
| `AVATAR_GIF_CHANNEL` | `"AC"` | `getShortAvatarUrl` の `nImageSize` |
| `AVATAR_GIF_BUST` (JP: Full) | `"F"` | `getAvatarUrl` の `nHalfCut` |
| `AVATAR_GIF_HALF` | `"H"` | `getShortAvatarUrl` の `nHalfCut` |
| `AVATAR_GIF_SUPPORT` (v3) | `"S"` | `aniChar = 'S'` |
| `AVATAR_GIF_SUPPORT` (v2) | `"A"` | `aniChar = 'A'` |
| `GetAvatarVersion()` | — | `getAvatarVersion()` (内部) |
| `CHgAniAvatar::getAvatarFileName()` | — | `getAvatarUrl()` / `getShortAvatarUrl()` |
