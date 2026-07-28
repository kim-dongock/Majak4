---
applyTo: 'server/**,client/**,scripts/**'
---

# AP-07 レーティング / レベル制限ルール

レガシー (`legacy/typing/`) 由来のユーザーレベル / レーティングに基づく UI 制限ルールをまとめる。
新しい画面・機能を実装する際は **必ずこのファイルを参照** し、レガシー挙動と差異が出ないようにすること。

---

## 1. レーティングシステム概要

- ユーザーのレーティングは `typing_rat` テーブル (`Rating` / `NLevel` / `SLevel` / `MatchCnt` 列) に保持する。
- レガシーソース: `legacy/typing/Server/source/HTpgRatingCommon.cpp` の `s_nTypingNLevel[]`
  - 28段階 (Level 0 〜 27) のしきい値配列。Level 5 (アーティスト) の境界が **30000**。
- 初回ログイン時は `Rating = 0`, `NLevel = 0` で `typing_rat` レコードを作成する。

### サーバ → クライアント

- `POST /auth/majak-login` のレスポンスに以下を **必ず含める**:
  - `rating`   (int) — `typing_rat.Rating`
  - `nLevel`   (int) — `typing_rat.NLevel`
  - `sLevel`   (int) — `typing_rat.SLevel`
  - `matchCnt` (int) — `typing_rat.MatchCnt`
- クライアント `App.tsx` のログインハンドラはこれらを `Player` オブジェクトにマップする。
  ハードコード値 (`rating: 0` 等) を残してはならない。

---

## 2. チャンネルグループ進入制限

### 唯一のソース・オブ・トゥルース

レガシー `legacy/typing/Resource/ClientSettingFiles/typinghgc.hsp` の `[List]` セクション:

```ini
NUM = 4
1=0091A,初心者ロビー,0,0,0,0,A
2=0090A,自由ロビー,0,0,0,0,A
3=0090B,自由ロビー2,0,0,0,0,A
4=00R3A,上級者ロビー,30000,0,0,0,A
```

フォーマット: `GroupId, GroupName, MinRating, MaxRating, MinAge, MaxAge, Sex`

⚠️ **注意**: DB の `typing_chanel_mast` には `min_rating` / `max_rating` 列は **存在しない**。
レガシー仕様どおり、サーバ側ハードコード (hsp の値の写し) で管理する。

### サーバ実装 (`server/src/Program.cs` `/api/channels`)

```csharp
// hsp [List] セクションの写し。新しいグループ追加時はここを更新すること。
static (int min, int max) GetGroupRatingThresholds(string subid) => subid switch
{
    "0091A" => (0,     0),   // 初心者ロビー
    "0090A" => (0,     0),   // 自由ロビー
    "0090B" => (0,     0),   // 自由ロビー2
    "00R3A" => (30000, 0),   // 上級者ロビー (Level 5 アーティスト以上)
    _       => (0,     0),
};
```

`/api/channels` のレスポンスは各グループに `minRating`, `maxRating`, `chanelType` を含む。

### クライアント判定ロジック

レガシー `Msg_JoinValidate` / `JPChannelGrpListCtrl::OnCheckChannelGrpState` と等価:

```ts
function isEnterable(group: { minRating: number; maxRating: number }, rating: number): boolean {
  if (group.minRating > 0 && rating < group.minRating) return false
  if (group.maxRating > 0 && rating > group.maxRating) return false
  return true
}
```

- `minRating === 0` → 下限なし
- `maxRating === 0` → 上限なし (`m_bEnterable` 判定でも 0 は無制限を意味)

### UI 表示ルール

- 進入不可グループ:
  - ボタンは `disabled` 属性 + `channelButtonDisabled` (グレー pill) スタイル
  - クリックは無視 (state を変更しない)
  - `title` ツールチップで必要レーティングを表示
- 画面初期化時は **進入可能な最初のグループ** を自動選択する (`data.find(g => isEnterable(g, rating))`)
  - 全グループ進入不可時のみ先頭グループを選択

---

## 3. 制限の適用範囲 (重要)

レガシー調査結果に基づき、**レベル / レーティング基準で制限される UI は以下のみ**:

| UI 要素                   | 制限種別          | 備考                                      |
|---------------------------|-------------------|-------------------------------------------|
| チャンネルグループ進入    | レーティング基準  | 本ドキュメントの第 2 節を参照             |
| 楽曲リストのコスト消費    | TPoint 残高基準   | レベル制限ではない (`cost_t_point`)       |
| ショップアイテム購入      | 所持金 / TPoint  | レベル制限ではない                        |
| ミッション開始            | 状態ベース        | 進行中ミッションの有無のみ                |
| ショップボタン            | 状態ベース        | レベル制限なし                            |

⚠️ **レベルや段位 (NLevel/SLevel) でボタンを非活性化するレガシー仕様は存在しない**。
新しい画面で「レベル X 以上で解放」のような独自仕様を勝手に追加してはいけない。
追加が必要な場合は必ずレガシーソース (`legacy/typing/Client/GAME/source/` または `.hml` / `.hsp`) で
裏付けを取ること。

---

## 4. 関連ファイル

- サーバ: [server/src/Program.cs](../../server/src/Program.cs) `/api/channels`, `/auth/majak-login`
- サーバ DB: [server/src/Infrastructure/TypingDbContext.cs](../../server/src/Infrastructure/TypingDbContext.cs) `TypingRat`
- クライアント型: [client/src/types/game.ts](../../client/src/types/game.ts) `ChannelGroup`, `Player`
- クライアント画面: [client/src/components/lobby/ChannelLobby.tsx](../../client/src/components/lobby/ChannelLobby.tsx)
- レガシー設定: `legacy/typing/Resource/ClientSettingFiles/typinghgc.hsp` `[List]` セクション
- レガシーレベル配列: `legacy/typing/Server/source/HTpgRatingCommon.cpp` `s_nTypingNLevel[]`
- レガシー進入判定: `JPChannelGrpListCtrl::OnCheckChannelGrpState`, `CChannelstGroupData::m_bEnterable`
