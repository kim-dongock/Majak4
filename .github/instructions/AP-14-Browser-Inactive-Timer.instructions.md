---
applyTo: "client/**,server/**"
description: "ブラウザ非アクティブ時のクライアントタイマー対策・再同期・遅延入力防止"
---

# AP-14 ブラウザ非アクティブ時のクライアントタイマー対策

## 目的

Web クライアントでは、タブが非アクティブになるとブラウザにより `setTimeout` / `setInterval` / `requestAnimationFrame` / Phaser `time.delayedCall` が遅延・停止される。

そのため、ゲーム進行に必要な制限時間・自動打牌・自動パスをクライアントタイマーだけに依存してはならない。本ドキュメントは、レガシーの通信順序・牌情報公開範囲を維持しながら、Web 版で必要な deadline / action sequence / 復帰同期を追加するための規約である。

---

## 1. レガシー根拠と Web 補強の分離

### 1-1. レガシーで確認できる事実

- レガシークライアントは操作受付開始時に制限タイマーを開始し、期限到達時に `MJPID_ACTION` を送信する。
- レガシーサーバーはタイマー単体で局・親・手番を直接進めない。必ず `MJPID_ACTION` または `ProxyPlay` 相当の既定アクションを engine に適用し、その結果として状態を進める。
- 切断プレイヤーや CPU/NPC など、入力が返らない対象には `ProxyPlay` を使う。`MODE_TURN` は手牌末尾 `TAP`、`MODE_FURO` / `MODE_CHAN` / `MODE_KYO` / `MODE_AGA` は `PAS` を実行する。
- `smmc4e` (`PaiInfoList`) は受信者ごとの公開範囲で送る。一般プレイヤーには自分に見える牌だけ、観戦者・練習・特殊公開条件では別 open mask を使う。
- 復帰・再入室時は、現在の `PaiInfo` と `history` を送り、クライアントが現在局を再構築する。
- `smmc4e` は単独で即時盤面適用しない。`PaiInfoStocker` に積み、対応する `playing` 処理時に同期して消費する。

### 1-2. Web 版で追加する補強

以下はレガシーにそのまま存在する仕様ではなく、ブラウザ非アクティブ・SignalR 再接続・遅延タイマー発火に対応するための Web 補強である。

- `MJPID_ACTIONS` に `actionSeq`, `serverNow`, `deadlineAt` を追加する。
- クライアントから送る `MJPID_ACTION` にも `actionSeq` を含める。
- サーバーは現在の pending action と一致しない `actionSeq` を stale input として無視する。
- クライアントは `deadlineAt` を過ぎた prompt から入力を送ってはならない。
- 非アクティブ復帰・focus 復帰・SignalR reconnect 後は、サーバー snapshot による再同期を行う。

---

## 2. 基本方針

### 2-1. ゲーム進行の権威

- サーバー engine が唯一のゲーム状態の権威である。
- クライアントは表示・入力送信・受信データ適用を担当するが、牌山・手番・局遷移・点棒・親交代を推測で進めてはならない。
- クライアントタイマーは UI 表示と早期入力送信の補助であり、ゲーム進行の成立条件にしてはならない。

### 2-3. 現行 Web 実装のサーバー主導ルール

- `GameLogicService` は実入力が必要な `MJPID_ACTIONS` を送るたびに pending action と deadline を保持し、deadline 到達時に server timeout fallback を実行する。
- `Furo` / `Chan` で `PAS` しか選択肢がない場合は実入力ではないため、`MJPID_ACTIONS` / pending action / deadline を作らず、サーバー内部で即時 `PAS` の `MJPID_ACTION` 相当を engine に適用する。
- timeout fallback は局・親・点棒を直接編集しない。`MODE_TURN` は手牌末尾 `TAP`、`MODE_FURO` / `MODE_CHAN` / `MODE_KYO` / `MODE_AGA` は `PAS` の `MJPID_ACTION` 相当を engine に渡す。
- `Furo` / `Chan` でロン・鳴きなど `PAS` 以外の選択肢がある場合だけ、接続中ユーザーへ deadline 付き prompt を出し、期限到達時に server timeout が既定 `PAS` を適用する。
- クライアントの自動打牌・自動パスは「早く送れる場合の補助」であり、非アクティブ・stocked replay・接続断で送れなくても進行は server timeout で継続する。
- 期限切れ、`actionSeq` 不一致、現在 prompt と違う入力は stale input として処理しない。これを理由に game state を巻き戻してはならない。

### 2-2. タイムアウトの扱い

- タイムアウト時も、サーバーは局状態を直接書き換えてはならない。
- サーバー timeout handler は `ProxyPlay` と同じ既定アクションを engine に適用する。
- `MODE_TURN` の既定アクションは手牌末尾 `TAP` とする。
- `MODE_FURO` / `MODE_CHAN` / `MODE_KYO` / `MODE_AGA` の既定アクションは `PAS` とする。
- timeout による既定アクション適用後の送信順序は通常アクションと同じく、`smmc4e` → `playing / MJPID_ACTION` → 必要な状態遷移 packet → 次の `MJPID_ACTIONS` とする。

---

## 3. `MJPID_ACTIONS` の deadline / actionSeq

### 3-1. サーバー送信 payload

Web 版の `MJPID_ACTIONS` には、従来の UI 用情報に加えて以下を必ず含める。
ただし `Furo` / `Chan` の pass-only 応答はサーバー内部で即時 `PAS` 解決するため、この payload を送らない。

```ts
{
  playType: "MJPID_ACTIONS",
  seatOrder: number,
  playerMode: "Turn" | "Furo" | "Chan" | "Kyo" | "Aga",
  actFlags: number,
  actions: unknown[],
  tapCandidates: number[],
  timeLimit: number,
  actionSeq: number,
  serverNow: number,
  deadlineAt: number
}
```

- `actionSeq`: room/game 内で単調増加する入力要求番号。
- `serverNow`: サーバーの現在時刻 (Unix epoch milliseconds)。
- `deadlineAt`: この入力要求の締切時刻 (Unix epoch milliseconds)。
- `timeLimit`: 表示用の秒数。判定には `deadlineAt` を使う。

### 3-2. サーバー側 pending state

サーバーは `MJPID_ACTIONS` 送信時に、少なくとも以下を room state に保存する。

```text
PendingActionSeq
PendingActionSeatOrder
PendingActionMode
PendingActionDeadlineAt
PendingActionIssuedAt
```

同時に複数プレイヤーの応答待ちが成立するルール状態では、`seatOrder` ごとの pending action として保持する。

### 3-3. クライアント送信 payload

クライアントの `MJPID_ACTION` には、受信した `actionSeq` を必ず含める。

```ts
{
  playType: "MJPID_ACTION",
  roomId: string | number,
  seatOrder: number,
  action: number,
  bipaiIndex: number[],
  actionSeq: number
}
```

---

## 4. 遅延入力の防止

### 4-1. クライアント送信前チェック

クライアントは、クリック・キーボード・自動打牌・自動パスのすべてで送信直前に次を確認する。

```ts
if (!currentPrompt) return
if (currentPrompt.actionSeq !== actionSeqToSend) return
if (performance.now() >= currentPrompt.localDeadlineAt) {
  clearActionUi()
  requestGameResync()
  return
}
```

- クライアントは `deadlineAt - serverNow` から残り時間を計算し、`localDeadlineAt = performance.now() + remainingMs` として保持する。
- `Date.now()` のような端末の壁時計だけで deadline を判定してはならない。ユーザー PC の時刻ずれや時刻変更の影響を受けるためである。
- `performance.now()` のような単調増加時計を使い、サーバーから受け取った残り時間をローカル deadline に変換する。
- deadline を過ぎた prompt からは、たとえ UI タイマーが遅延発火しても送信してはならない。
- deadline 経過後は入力 UI を閉じ、サーバー再同期を要求する。

### 4-2. サーバー受信前チェック

サーバーは `MJPID_ACTION` を engine に渡す前に、必ず次を検証する。

```text
seatOrder が送信者の engine order と一致する
actionSeq が現在の pending action と一致する
現在時刻が PendingActionDeadlineAt を超過していない
playerMode と action が現在の engine state で有効である
bipaiIndex が現在の有効候補に含まれる
```

一致しない場合は stale input として無視する。SignalR 接続を落としたり、局状態を巻き戻したりしてはならない。

### 4-3. サーバー timeout task

`MJPID_ACTIONS` 送信時、サーバーは deadline 到達時に既定アクションを実行する timeout task を予約する。

timeout task は実行直前に必ず再検証する。

```text
room が存在する
game が終了していない
pending actionSeq が予約時と同じ
対象 playerMode が予約時と同じ
engine state で既定アクションがまだ有効
```

検証に失敗した場合は何もしない。既にクライアント入力で処理済み、または次の prompt に進んだものとして扱う。

---

## 5. 非アクティブ復帰・再接続時の同期

### 5-1. 復帰トリガー

クライアントは以下のタイミングで再同期を要求する。

- `document.visibilitychange` で visible に戻った時。
- window `focus` に戻った時。
- SignalR `onreconnected` 後。
- `deadlineAt` を過ぎた current prompt を検出した時。
- SignalR の stocked packet replay が失敗、または sequence gap を検出した時。

### 5-2. 復帰時のクライアント処理

復帰開始時、クライアントはまず入力 UI を閉じる。

```text
clearActionButtons
clearAutoDiscardTimer
clearActionResponseTimer
canDiscardOnTileClick = false
currentActionSeatOrder = null
```

その後、サーバーに現在 room/game の snapshot を要求する。

### 5-2-1. 退室を選んだ場合の abandon flow

接続断後にユーザーが退室を選んだ場合、`c9e` (`commandExitRoom`) は既に送れないことがある。この場合は次のロビー入場で退室意図をサーバーへ伝える。

- `RoomScreen.exitRoomToLobby` は `c9e` 送信に失敗したら `sessionStorage` に `{ channelId, roomId }` を保存する。
- `LobbyScreen` は次回 `c1e` (`EnterChannel`) payload に `abandonPreviousRoom=true` と `abandonRoomId` を含める。
- `EnterChannelCommand` は、該当 room が同一 channel の Playing room で、同一 memberId の座席を持つ場合だけ abandon を許可する。
- abandon 時、サーバーは座席を削除せず `IsOutPlayer=true` とし、古い connection/member mapping を外してロビー入場を許可する。`USER_MULTI_LOGIN` を返してタイトルへ戻してはならない。
- abandon 指定がない場合、同一 memberId が active player として残っていれば従来通り duplicate login として扱う。`IsOutPlayer=true` の同一 memberId は復帰対象である。

### 5-3. snapshot の構成

snapshot は受信者の公開範囲に合わせて構築する。

```ts
{
  roomId: number,
  gameSeq: number,
  serverNow: number,
  publicState: {
    kyokuCnt: number,
    chicha: number,
    oyaOrder: number,
    leftCount: number,
    riboCnt: number,
    renChanCnt: number,
    players: Array<{
      seatOrder: number,
      memberId: string,
      score: number,
      handCount: number,
      discards: unknown[],
      melds: unknown[],
      reach: boolean
    }>
  },
  ownPrivateState?: {
    seatOrder: number,
    paiInfo: Array<{ code: number, idx: number, red?: boolean }>
  },
  currentPrompt?: {
    actionSeq: number,
    seatOrder: number,
    playerMode: string,
    actions: unknown[],
    tapCandidates: number[],
    deadlineAt: number,
    timeLimit: number
  },
  history?: unknown[]
}
```

- 一般プレイヤーには他家の非公開手牌コードを送ってはならない。
- 他家については公開牌・副露・捨て牌・手牌枚数のみ同期する。
- 自分の手牌・`bipaiIndex` は `ownPrivateState` として送る。
- 観戦者・練習・特殊公開条件では、レガシー open mask に従って公開範囲を広げる。

### 5-4. snapshot 適用後の扱い

- snapshot 適用時は、古い local action prompt と古い PaiInfo queue を破棄する。
- snapshot に `currentPrompt` があり、`deadlineAt` がまだ有効なら入力 UI を再表示する。
- `deadlineAt` が過ぎている場合は入力 UI を出さず、サーバー timeout / 次 packet を待つ。
- snapshot は packet order を変更するためのものではない。通常受信中は AP-05 の `smmc4e` → `playing` 同期規約を維持する。

---

## 6. 通常 packet queue と snapshot の関係

- 通常通信ではサーバー到着順の単一 FIFO として `smmc4e` / `playing` / `history` を扱う。
- コマンド種別ごとの別 queue で replay してはならない。
- `smmc4e` は `PaiInfoStocker` 相当へ積み、対応する `playing` 処理時に消費する。
- 長時間非アクティブ・再接続・sequence gap などで完全な replay が保証できない場合は、古い queue の完全再生にこだわらず snapshot で現在状態へ合わせる。
- snapshot 適用後に古い stocked packet を replay してはならない。

---

## 7. 実装対象の目安

### server

- `GameLogicService.SendValidActionsToPlayersAsync` で `actionSeq`, `serverNow`, `deadlineAt` を付与する。
- `Furo` / `Chan` の pass-only 応答は `MJPID_ACTIONS` 送信前にサーバー内部 `PAS` として処理し、クライアント prompt を作らない。
- room state に pending action を保存する。
- `GamePlayCommand.ExecuteAsync` から engine 処理へ入る前に `actionSeq` / deadline / current mode を検証する。
- timeout task は `ProxyPlayAsync` と同じ既定アクション経路で処理する。
- 復帰 snapshot API または SignalR hub method を追加し、受信者別公開範囲で `PaiInfo` / `history` / `currentPrompt` を返す。
- `PlayerSessionService.Remove(connectionId)` は、削除対象 connectionId が現在の member mapping と一致する場合だけ `memberId -> connectionId` を削除する。古い切断イベントが新しい接続を消す race を作ってはならない。
- `PlayerSessionService.DisconnectFromRoom` は対局中プレイヤーの座席/`RoomId` を保持し、古い connection mapping だけを切り離す。復帰は元座席へ戻す。

### client

- `MJPID_ACTIONS` 受信時に `actionSeq`, `serverNow`, `deadlineAt` を保存する。
- `serverTimeOffsetMs` を更新し、送信直前 deadline 判定に使う。
- `discard`, `sendAction`, `scheduleAutoDiscard`, `scheduleDefaultPass` は送信直前に `actionSeq` と deadline を再検証する。
- `visibilitychange`, `focus`, SignalR reconnect で入力 UI を閉じ、snapshot 再同期を要求する。
- snapshot 適用後は古い action prompt / stocked packet / PaiInfo queue を破棄する。
- 接続断後にユーザーが退室した場合、`c9e` 失敗を握りつぶすだけにせず、次の `c1e` に abandon intent を含める。

---

## 8. 禁止事項

- クライアント timer の期限到達だけを根拠に局・親・手番を進めてはならない。
- deadline を過ぎた `MJPID_ACTION` をサーバーで処理してはならない。
- `actionSeq` が一致しない stale input を engine に渡してはならない。
- 一般プレイヤーへ他家の非公開手牌コードを snapshot で送ってはならない。
- snapshot 後に古い stocked packet を replay してはならない。
- Web 補強を理由に AP-05 の通常 packet order を変更してはならない。
