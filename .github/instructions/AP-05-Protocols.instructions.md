---
applyTo: "server/**,client/**,scripts/**"
description: "麻雀4のSignalR・RESTプロトコル、レガシー互換キー、認証済みID、対局開始・復帰シーケンスを確認・変更するときに参照する"
---

# AP-05 プロトコル定義 (Legacy Majak2)

## 目的

- レガシーMajak2サーバー (C++) の通信プロトコルをASP.NET Core/.NET 8移植向けに整理する。
- コマンド一覧、主なリクエストパラメータ、主なレスポンス/戻り値を明文化する。
- 本ドキュメントは実装仕様の一次資料として扱い、差分が出た場合はコード優先で更新する。

### ID表現

- レガシーのプロトコルキー名 `G::keyMemberId` / `keyMemberId{i}` は互換のため変更しないが、現行Webでクライアントへ送受信する値は原則 `pix` である。
- DB・Redisキー・サーバー内部セッションで本人を表す値は `player_account.member_no` であり、JWTの `member_no` から確定する。
- コマンドpayloadの `memberId` / `memberNo` / `pix` を内部 `member_no` と同一視してはならない。本人操作は `CommandContext.AuthMemberNo`、公開識別は `AuthPix` を使う。

## 参照ソース

- `Majak4_legacy/server/server/HMajProtocol.h`
- `Majak4_legacy/server/server/HMajProtocol.cpp`
- `Majak4_legacy/server/server/HMajChnlServer.cpp` / `HMajChnlServer.h`
- `Majak4_legacy/server/server/HMajRoomServer.cpp` / `HMajRoomServer.h`
- `Majak4_legacy/server/server/HMajChnlInfo.h`
- `Majak4_legacy/server/server/MajakDef.h`

---

## サービス一覧

| 論理名 | サービスコード | 用途 |
|---|---|---|
| Majak2 | `majak2` | チャンネル全般 (主要サービス) |
| RoomControl | `smmc*e` / 特殊文字列 | ルーム内ゲーム進行 |

---

## コマンド一覧

### 1) チャンネルコマンド (`mjkc*e`)

| No | シンボル名 | コード | 主処理 |
|---|---|---|---|
| 1 | commandMajGetDetailRec | `mjkc1e` | 詳細戦績取得 |
| 2 | commandMajAutoMatching | `mjkc2e` | オートマッチング要求 |
| 3 | commandMajCancelAutoMatching | `mjkc3e` | オートマッチングキャンセル |
| 4 | commandMajAutoStart | `mjkc4e` | オート開始 |
| 5 | commandMajAutoExitRoom | `mjkc5e` | オート退室 |
| 6 | commandMajAutoEnterRoom | `mjkc6e` | オート入室 |
| 13 | commandMajChannelStop | `mjkc13e` | チャンネル停止 |
| 14 | commandMajGetServerTime | `mjkc14e` | サーバー時刻取得 |
| 16 | commandAvatarGear | `mjkc16e` | アバターギア取得 (役満/リーチ一発ツモ等) |
| 17 | commandMoneyReplenishment | `mjkc17e` | コイン補充 (オールイン) |
| 18 | commandApplyEarnedMoney | `mjkc18e` | 獲得コイン適用 |
| 19 | commandGetTitle | `mjkc19e` | 称号取得 |
| 20 | commandBuyMajItem | `mjkc20e` | アイテム購入 |
| 21 | commandSelectMajItem | `mjkc21e` | アイテム選択 |
| 22 | commandGetGem | `mjkc22e` | ジェム (龍宝石) 取得 |
| 23 | commandYakumanBonus | `mjkc23e` | 役満ボーナス付与 |
| 24 | commandUseEmoticon | `mjkc24e` | エモート使用 |
| 25 | commandRatingRankInfo | `mjkc25e` | レーティングランキング情報取得 |
| 26 | commandTournamentList | `mjkc26e` | トーナメント一覧 |
| 27 | commandTournamentRegist | `mjkc27e` | トーナメント登録 |
| 28 | commandTournamentJoin | `mjkc28e` | トーナメント参加 |
| 29 | commandTournamentJoinCancel | `mjkc29e` | トーナメント参加キャンセル |
| 30 | commandTournamentDetail | `mjkc30e` | トーナメント詳細 |
| 31 | commandDeliveryMessage | `mjkc31e` | 配信メッセージ取得 |
| 32 | commandGetMissionList | `mjkc32e` | ミッションリスト取得 |
| 33 | commandRcvWeeklyReward | `mjkc33e` | 週間報酬受取 |
| 34 | commandRcvSerialBonus | `mjkc34e` | シリアルボーナス受取 |
| 35 | commandShopItemRequest | `mjkc35e` | カスタムショップ一覧要求 |
| 36 | commandShopItemResponse | `mjkc36e` | カスタムショップ一覧応答 |
| 37 | commandSetCustomItem | `mjkc37e` | カスタムアイテム設定 |
| 38 | commandEquipCustomItem | `mjkc38e` | カスタムアイテム装備確定 |
| 39 | commandCustomItem | `mjkc39e` | 所持アイテム要求 |
| 40 | commandCustomItemResponse | `mjkc40e` | 所持アイテム一覧応答 |
| 41 | commandBuyCustomItem | `mjkc41e` | アイテム購入要求 |
| 42 | commandBuyCustomItemResponse | `mjkc42e` | アイテム購入応答 |

### 2) ルーム/ゲームコマンド (`smmc*e` + 特殊文字列)

| シンボル名 | コード | 主処理 |
|---|---|---|
| commandSendOkButton | `smmc1e` | OKボタン送信 (ゲーム開始同意) |
| commandPushOkButton | `smmc2e` | OKボタン押下 (相手通知) |
| commandEventInfo | `smmc3e` | イベント情報取得 |
| commandPaiInfoList | `smmc4e` | 牌情報リスト |
| commandTsumikomi | `smmc5e` | 積み込み (チート検証用) |
| commandIPAdapterInfo | `smmc6e` | IPアダプター情報 |
| commandRoomState | `mjkroom` | ルーム状態通知 |
| commandGamePlay | `playing` | ゲームプレイ中パケット |
| commandAgariRec | `horarec` | 和了記録 |
| commandHistory | `history` | 履歴参照 |
| commandReplayNavi | `repnavi` | リプレイナビ |
| Web forced logout | `forcedLogout` | Web 版のサーバー主導ログアウト通知 |

---

## 主要コマンド仕様 (移植対象)

### チャンネル系

| コマンド | リクエスト主要キー | レスポンス主要キー | 処理クラス |
|---|---|---|---|
| `mjkc1e` GetDetailRec | `G::keyMemberId` | `keyTurnCnt`〜`keyRichiHoraCnt` (戦績統計群) | `ProcessCommand_DetailRec` |
| `mjkc2e` AutoMatching | (なし) | `G::keyResult` | `ProcessCommand_AutoMatching` |
| `mjkc3e` CancelAutoMatching | (なし) | `G::keyResult` | `ProcessCommand_CancelAutoMatching` |
| `mjkc17e` MoneyReplenishment | `keyReplenishmentType` | `G::keyResult`, `G::keyGamMoney`, `keyLentMoney`, `keyRestAllInCnt` | `ProcessCommand_MoneyReplenishment` |
| `mjkc18e` ApplyEarnedMoney | (なし) | `G::keyResult`, `G::keyGamMoney` | `ProcessCommand_ApplyEarnedMoney` |
| `mjkc19e` GetTitle | `keyTitleType`, `keyTitleCode` | `G::keyResult`, `keyTrickTitle`, `keyMajakTitle` | — |
| `mjkc22e` GetGem | `keySellCode`, `keyItemCode` | `G::keyResult`, `keyGemCount` | — |
| `mjkc24e` UseEmoticon | `keyEmoticonId` | `G::keyMemberId`, `keyEmoticonAvatarId` (ブロードキャスト) | — |
| `mjkc25e` RatingRankInfo | `G::keyMemberId` | グレードランキング群 (`keyGradeRankList`等) | `ProcessCommand_RatingRankInfo` |
| `mjkc32e` GetMissionList | `G::keyMemberId` | `keyDailyMission1`〜`keyDailyMission11`, `keyWeeklyReward1`〜`keyWeeklyReward8` | — |
| `mjkc33e` RcvWeeklyReward | `keyWeeklyRewardId` | `G::keyResult`, `G::keyGamMoney` | — |
| `mjkc34e` RcvSerialBonus | `keySerialCode` | `G::keyResult` | — |
| `mjkc35e` ShopItemRequest | (なし) | `mjkc36e` で応答 (カスタムショップ一覧) | — |
| `mjkc41e` BuyCustomItem | `keyCustomId`, `keyShopNo` | `mjkc42e` で応答 (`G::keyResult`, `keyItemQuantity`) | — |

#### 無料GP補充 (`mjkc17e`)

- 公式Webマニュアル `5_3` を優先し、プレイヤーの段位、接続元、ネットカフェ判定による例外を設けない。
- 所持GPが1,000未満で、午前6時を境界とする当日分を未使用の場合だけ成功する。
- 成功時は差額だけを付与して残高を正確に1,000 GPとし、当日の使用回数を1にする。
- `keyReplenishmentType` は成功時 `2`、当日使用済みの場合 `3` とする。
- 成功応答はチャンネルへ配信されるため、クライアントは応答の対象 `pix` がログイン中の本人の場合だけ自身のGP残高を更新する。

#### コレクション REST API

- `GET /api/player/collection` はゲームJWTの本人について、獲得済みの麻雀称号 (`mjkt*` / `mjkc*`) と技 (`mjks*`) および各装着状態を返す。
- `POST /api/player/collection/equip` は `{ category: "majak" | "trick", titleId: string | null }` を受け取る。`titleId=null` は指定カテゴリの装着解除を表す。
- リクエストの会員番号は受け取らず、ゲームJWTから確定する。装着できるのは本人が有効な状態で獲得済みの、指定カテゴリに一致する称号または技だけとする。
- 麻雀称号と技はそれぞれ最大1つを装着でき、変更時はDBと接続中の `MajakPlayer` を同時に更新する。
- `mjkc19e` は対局中の新規獲得通知専用であり、コレクションの参照・装着変更には使用しない。

#### ロビーメンバー一覧 (`c7e`)

- 構造化 `members[]` は `sex` / `k11e`、`age` / `k10e`、`nlevel` / `k33e` を含める。
- `age` はサーバーが `player_account.birth_year` と現在年から算出する。出生年そのものを他プレイヤーへ送らない。
- 出生年未設定の既存会員は `age=0` とし、「すべて」では表示するが特定の年齢帯フィルターには一致させない。
- クライアントの性別・年齢帯・資産レベル／段位フィルターは、指定された全条件を AND で適用する。

### ゲーム進行系 (ルーム内)

| コマンド | 主処理 |
|---|---|
| `smmc1e` SendOkButton | ゲーム開始前のOK合意状態通知。OK 押下後に全員へ現在の OK 状態を送る |
| `smmc4e` PaiInfoList | 牌山・手牌の初期配布情報を送信 |
| `mjkroom` RoomState | ルーム状態変化 (入退室・座席移動・ゲーム開始/終了) を全員へ通知 |
| `playing` GamePlay | 打牌・鳴き・リーチ・和了などゲーム内アクションを中継 |
| `horarec` AgariRec | 和了成立時の点数計算結果を全員へ通知 |
| `history` History | 局終了後の履歴 (各局の点棒移動) |
| `repnavi` ReplayNavi | リプレイ再生ナビゲーション |

---

### ゲーム開始・進行パケット順序 (最重要)

この節はコマンド一覧ではなく、サーバー送信順とクライアント消費順の契約である。移植では「どのコマンドを送るか」だけでなく「どの順で受け取り、いつ適用するか」をレガシーと一致させる。

レガシー根拠:

- サーバー開始同意: `Majak4_legacy/server/server/HMajRoomServer.cpp::ProcessCommand_PushOkButton`
  - OK 状態更新後 `SendOkButtonState()` (`smmc1e`) を送信。
  - 送信者へ `smmc2e` 応答を返す。
  - 全員 OK 後に `StartGameProcess()` → `StartGameLogic()` をこの順で呼ぶ。
- サーバー自動開始通知: `HMajRoomServer::AutoJoinRoom` / `StartGameProcess` 呼び出し点
  - `AutoJoinRoom` では `commandMajAutoStart` (`mjkc4e`) を全員へ送信してから `StartGameLogic()` を呼ぶ。
  - OK 開始経路では `ProcessCommand_PushOkButton` が `StartGameProcess()` を `StartGameLogic()` より前に呼ぶ。ただし、このアーカイブ内では `StartGameProcess()` 本体は確認できないため、移植仕様としては `mjkc4e` が `MJPID_INIHAN` より前に届くことを必須条件にする。
  - `mjkc4e` には `AddToParser_AutoStartResponse()` による対局メンバー情報と `onStartNewGame()` 情報が含まれる。
- サーバー局開始本体: `HMajRoomServer::StartGameLogic`
  - `majak.InitHanchan(rule)`
  - `playing` / `MJPID_INIHAN` (`AddToParser_HanchanInfo`)
  - `FindPlayerByOrder(nOrder)` で `m_stHanchanInfo.m_nPlayer[nOrder]` (room/player position) に対応する `HMajPlayer` を探し、各プレイヤーの `m_nSeatPos = nOrder` (engine order) を確定
  - `smmc4e` (`SendPaiInfoToAll`, 初回は `bInit=true`)
  - `playing` / `MJPID_INIKYO` (`OnInitKyoku`)
- クライアント受信: `Majak4_legacy/client/client/HgMajak2/MJRoomWnd3.cpp::DispatchServices`
  - `mjkc4e` は `ProcessRoomStartNewGameCommand()` で `MODE_PLAYING` に入り、卓を準備する。
  - `playing` は `m_tbl.OnRecvPlay()` へ渡す。
  - `smmc4e` は `m_tbl.OnRecvPaiInfo()` へ渡す。
- クライアント牌情報同期: `MJTblComm1.cpp::RecvPlay` / `PaiInfoMsgStocker.cpp`
  - `smmc4e` は即時に独立処理せず `PaiInfoStocker` に積む。
  - `RecvPlay()` が `playing` を取り出す時に `m_PaiInfoMsgStocker.pop(typ == MJPID_INIKYO, ...)` で対応する牌情報を適用する。
  - 初回 `bInit=true` の PaiInfo は `MJPID_INIKYO` 処理時に同期して取り出される。

サーバー/クライアント相互作用の契約:

| フェーズ | サーバー責務 | クライアント責務 |
|---|---|---|
| 準備 OK | `smmc2e` を受けたら ready 状態を更新し、`smmc1e` で全員の OK 状態を通知し、押下者へ `smmc2e` 応答を返す。 | `smmc1e` は表示状態更新、`smmc2e` 応答は自分の押下結果として扱う。これだけで卓初期化を始めない。 |
| 対局開始通知 | 全員 OK / AutoJoin 成立後、`mjkc4e` を `INIHAN` より前に送る。payload には対局メンバー、ホスト、BAN 状態、`onStartNewGame()` 情報を含める。 | `mjkc4e` で `MODE_PLAYING` に入り、卓・メンバー・表示を準備する。まだ局データは確定扱いしない。 |
| 半荘初期化 | `majak.InitHanchan(rule)` 後に `playing / MJPID_INIHAN` を全員へ送る。 | 半荘情報を受け取り、親・エンジン order・対局者対応を初期化する。 |
| 初期牌情報 | engine order と player pos を確定後、各プレイヤーごとに `smmc4e(bInit=true)` を送る。観戦者/全公開は open mask が異なる。 | `smmc4e` は即時盤面適用しない。PaiInfo キューに積み、対応する `playing` 受信時に消費する。 |
| 局開始 | `playing / MJPID_INIKYO` を送る。Web 版の `MJPID_ACTIONS` はこの後にだけ送る。 | `MJPID_INIKYO` 処理時に `bInit=true` の PaiInfo を取り出して盤面へ適用し、局状態を開始する。 |
| プレイ中アクション | C->S `playing / MJPID_ACTION` を engine で検証・適用した後、S->C `smmc4e` → `playing / MJPID_ACTION` → 必要なら `INIKYO`/`ENDKYO`/終局 → `MJPID_ACTIONS` の順で送る。 | `smmc4e` は次の `playing` と同期して適用する。`MJPID_ACTION` は実行履歴、`MJPID_ACTIONS` は Web UI の有効操作通知として分離する。 |

親・手番・進行の契約:

- `MJPID_INIHAN` (`DATAFORMAT_HANCHANINFO`) は半荘の基準情報であり、`nChicha` (起家) と `nPlayer[4]` (engine order → room/player position) を含む。
- レガシーでは `m_nPlayerPos` (room/player position) と `m_nSeatPos` (engine order) は別物である。移植先でも room 座席フィールドを engine order で上書きしてはならない。C# では例として `SeatPos` は room/player position、`EngineOrder` は `m_nSeatPos` 相当として分離する。
- `MJPID_INIKYO` (`DATAFORMAT_KYOKUINFO`) は局の開始情報であり、`nKyokuCnt`, `nRiboCnt`, `nRenChanCnt`, `nDice[2]`, `nMemberPoint[4]`, `bYakitori[4]`, `nTip[4]` を含む。
- レガシークライアントは `MJPID_INIHAN` で `m_nChicha` を保存し、`MJPID_INIKYO` で `m_nCurKyo` を受けた後、`m_nOyaOdr = (m_nChicha + m_nCurKyo) % 4` として現在局の親を決める。局開始時の現在手番も同じ基準で `m_nCurOdr = (m_nChicha + m_nCurKyo) % 4` から始まる。
- 以後の C->S / S->C `MJPID_ACTION` は `DATAFORMAT_ACTIONINFO_RES` (`nSeatOrder`, `eAction`, `nBipaiIndex[]`) で送る。tile code フィールドは存在しない。サーバーは `nSeatOrder == pPlayer->m_nSeatPos` を検証してから engine に適用する。
- `MJPID_ACTION` の打牌コードは packet に追加して解決してはならない。レガシークライアントは `RecvPlay()` で直前の `smmc4e` を適用し、`nBipaiIndex[]` を現在手牌内の index に変換して `m_CurPai = pp->GetHandPai(idx[0])` から牌コードを得る。
- サーバーは action 適用後、牌変化を `smmc4e`、実行アクションを `playing / MJPID_ACTION` としてこの順で通知する。局が進む場合だけ続けて `MJPID_INIKYO`、局終了の場合は終局処理を送る。
- Web 版の有効操作提示 (`MJPID_ACTIONS`) は、レガシーの実行アクションではなく UI 補助である。親・手番・可能操作はサーバー engine 状態から導出し、クライアント推測で先に進めてはならない。
- `MJPID_ACTIONS.playerMode == Turn` の時だけ「自摸番/打牌番」としてターン表示・打牌タイマーを出す。`Furo` / `Chan` は捨て牌に対する鳴き・ロン・パスの応答待ちであり、全員が次の自摸番になったわけではない。
- `Furo` / `Chan` で `PAS` しか選択肢がない場合は、Web UI 上でターン表示や打牌タイマーを出してはならない。Web 版ではこの「選択不能な応答待ち」はクライアントに `MJPID_ACTIONS` を送らず、サーバー内部で `PAS` の `MJPID_ACTION` 相当を engine に適用する。
- pass-only `Furo` / `Chan` のサーバー内部 `PAS` も通常アクションと同じく、必要な `smmc4e`、`playing / MJPID_ACTION(PAS)`、状態遷移 packet、次の `MJPID_ACTIONS` の順で通知する。クライアントは実行済み `MJPID_ACTION(PAS)` を履歴として処理し、入力 prompt として扱ってはならない。

制限時間・自動操作の契約:

- レガシークライアントは操作受付開始時に `CMJGameWnd::StartLimitTimer()` を開始し、`TID_LIMIT` で残り時間を更新する。
- 時間切れ時はクライアントが `CMJGameWnd::ExeAct()` 経由で既定アクションを C->S `playing / MJPID_ACTION` として送る。`TID_LIMIT` では自動和了設定時に `RON` / `TSU` が優先され、通常の期限到達では `ExeAct(PAS, true)`、遅延実行では `ExeAct(INV, true)` が呼ばれる。
- サーバーは「時間切れだから親を進める」処理を直接行わない。必ず受信した `MJPID_ACTION`、または Web 補強の server timeout が作る既定 `MJPID_ACTION` 相当を engine に適用し、その結果として手番・局・親が進む。
- `Furo` / `Chan` でロン・鳴きなど `PAS` 以外の選択肢がある場合だけ、接続中ユーザーへ `MJPID_ACTIONS` と `deadlineAt` を送って入力待ちする。
- `Furo` / `Chan` が pass-only の場合は入力待ちにしてはならない。サーバーは prompt/deadline を作らず即時に既定 `PAS` を engine に適用し、クライアント側の自動パス送信や非アクティブ timer に依存させない。
- 切断プレイヤーや CPU/NPC などクライアントから入力が返らない場合だけ `ProxyPlay` 相当を使う。レガシー server `ProxyPlay` は `MODE_TURN` なら手牌末尾を `TAP`、`MODE_FURO` / `MODE_CHAN` / `MODE_KYO` / `MODE_AGA` なら `PAS` を実行する。`ProxyPlay(nOrder)` の `nOrder` は engine order であり、room/player position ではない。
- 親交代はタイマー単体ではなく、engine が局終了・次局開始に到達して次の `MJPID_INIKYO` を送った時に確定する。クライアントは `MJPID_INIKYO.nKyokuCnt` と保存済み `nChicha` から `m_nOyaOdr = (m_nChicha + m_nCurKyo) % 4` を再計算する。
- Web 版で `MJPID_ACTIONS.timeLimit` を使う場合、`timeLimit` は表示用であり判定は `deadlineAt` / `actionSeq` を使う。クライアントが期限前に `MJPID_ACTION` を送れない場合でも、server timeout が同じ既定アクションを適用するため、ゲーム進行をクライアント timer に依存させてはならない。
- Web 版の `MJPID_ACTIONS` は `baseTimeMs`, `keepTimeMs`, `timeBankMs`, `timeBankEnabled` も送る。`Turn` は発行時から局持ち時間を使用可能とし、deadline は `baseTimeMs + timeBankMs` とする。`Furo` / `Chan` は最初は基本時間だけを deadline とし、局持ち時間を使う場合は C->S `playing / MJPID_EXTEND_TIME_BANK` (`seatOrder`, `actionSeq`) を明示的に送る。
- S->C `playing / MJPID_TIME_BANK_EXTENDED` は同じ `actionSeq` の新しい `serverNow`, `deadlineAt` と timing fields を返す。拡張は同一 prompt につき1回だけ許可し、古い timeout task は prompt identity の不一致により無効化する。
- 局持ち時間は操作完了までの経過時間から `baseTimeMs` を超えた分だけ差し引く。基本時間内の操作では消費せず、局開始時に speed preset の `full - turn` へリセットする。

順序の読み方:

- レガシーの正は「サーバーが送った到着順」であり、コマンド種別ごとの優先順位ではない。
- クライアントは `smmc4e` を受信時点で盤面へ即時適用してはならない。レガシー `PaiInfoStocker::push()` と同じく PaiInfo 専用キューに積み、`RecvPlay()` が次の `playing` を処理する時に `pop(typ == MJPID_INIKYO, ...)` 相当で同期適用する。
- `PaiInfoStocker::push()` はキューが空、または `bInit=true` の時に新しい PaiInfo メッセージを作る。`bInit=false` の追加 PaiInfo は現在の末尾メッセージへ連結される。
- `PaiInfoStocker::pop(false, ...)` は先頭が初期局 (`bIniKyo=true`) の場合は消費しない。`PaiInfoStocker::pop(true, ...)` は先頭が初期局であることを前提に消費する。
- `smmc4e` の PaiInfo は「手牌リスト」ではなく、Bipai index ごとの牌コード公開情報である。レガシークライアントは `ApplyPaiInfo()` / `m_pGame->SetBipai()` で Bipai 情報を更新し、手牌そのものは `MJPID_INIKYO` / `MJPID_ACTION` を処理するゲーム進行ロジック側で構成する。
- Web 版で一時的に PaiInfo から手牌表示を補完する場合でも、`smmc4e.pai` を全件そのまま「捨てられる手牌」とみなしてはならない。打牌可能な `bipaiIndex` はサーバー engine が提示する `MJPID_ACTIONS` / Tap 候補、またはレガシーと同等の手牌構成ロジックで確定する。
- `smmc4e(bInit=true)` は `MJPID_INIKYO` に対応する初期配牌である。`MJPID_INIHAN` 受信時に消費してはならない。
- Web 版のゲーム画面準備 ACK は `mjkc4e` と `MJPID_INIHAN` の間でサーバー送信開始を遅らせる補助であり、レガシーの packet order を変更する権限を持たない。
- サーバーが状態の唯一の権威である。クライアントは UI 準備・入力送信・受信データ適用を担当し、牌山・点棒・局遷移をクライアント側推測で進めてはならない。

通常ゲーム開始時の順序:

```text
C->S: smmc2e (OK ボタン)
S->C: smmc1e (全員の OK 状態)
S->C: smmc2e (押下者への OK 応答)
S->C: mjkc4e (AutoStart: メンバー情報・ゲーム開始準備)
S->C: playing / MJPID_INIHAN (半荘情報)
S->C: smmc4e (PaiInfoList, bInit=true: 初期配牌情報)
S->C: playing / MJPID_INIKYO (局情報)
S->C: playing / MJPID_ACTIONS (Web 移植の有効操作通知。必ず INIKYO 後)
```

通常アクション後の順序:

```text
C->S: playing / MJPID_ACTION (打牌・鳴き・和了など)
S->C: smmc4e (PaiInfoList: 牌変化)
S->C: playing / MJPID_ACTION (実行アクション)
S->C: playing / MJPID_INIKYO または MJPID_ENDKYO または終局処理 (状態遷移時のみ)
S->C: playing / MJPID_ACTIONS (次に操作可能なプレイヤーへの Web 有効操作通知)
```

移植ルール:

- `mjkc4e` より前に `MJPID_INIHAN` / `smmc4e` / `MJPID_INIKYO` を送ってはならない。
- `MJPID_INIHAN` / 初回 `smmc4e` / `MJPID_INIKYO` の順序を入れ替えてはならない。
- `MJPID_TURN` のような Web 独自の手番 packet を追加してはならない。手番表示は `MJPID_INIKYO` の親・現在局情報と `MJPID_ACTIONS.playerMode == Turn` から導出する。
- Web 版で Phaser/React の準備待ち ACK を追加する場合でも、ACK は送信開始タイミングを遅らせるだけの Web 移植補助であり、レガシー順序そのものを変更してはならない。
- クライアントで `playing` と `smmc4e` を一時保存する場合、コマンド別キューで再生してはならない。サーバー到着順を保つ単一 FIFO キューで再生する。
- `smmc4e.openPos` は「この PaiInfo が開示された engine order」であり、常に自分の表示基準 (`myOdr`) ではない。受信対象が自分の `pix` と一致することを確認せず `myOdr` を上書きしてはならない。
- ルーム画面内で `mjkc4e` を受けてインライン卓を起動する場合、卓起動直後に `c14e` (`EnterRoom`) を再送してはならない。対局中の `c14e` は落ち戻り/再入室系の経路であり、サーバーは `smmc4e(bInit=true)` と `history` を再送するため、通常開始シーケンスに混ぜると初期配牌・履歴が二重適用される。
- `history` は通常開始シーケンスではない。対局中再入室・観戦・復帰時の再構築用として扱う。
- `MJPID_ACTIONS` は Web 移植で追加した UI 用の有効操作通知であり、レガシーの `MJPID_ACTION` 実行通知とは別物として扱う。`MJPID_ACTIONS` を受けても実行アクション履歴に追加してはならない。

---

### 接続断・落ち戻り (ユーザー接続復帰フロー)

レガシー根拠:

- クライアント room socket close: `Majak4_legacy/client/client/HgMajak2/MJRoomWnd1.cpp::CMJRoomWnd::OnSocketClose`
  - `CHgGameWnd::OnSocketClose()` がエラーを返した場合は `ForceExit()` する。
  - Web 版でもルーム画面に留めず、現在チャンネルのトップへ戻す。通信が切れている画面を操作可能に見せてはならない。
- サーバー room socket close: `Majak4_legacy/server/server/HMajRoomServer.cpp::DispatchRoomSocketClose`
  - 対局中 (`PS_PLAY` / `PS_CONTINUE`) の異常切断は完全退室ではない。
  - プレイヤーは out/continue player として保持し、通常の空席扱いにしない。
- チャンネル再入場: `Majak4_legacy/client/client/HgChnlM/HgChannelWnd.cpp::SendEnterChannel`
  - `MAJ::keyIsContinue` (`mjkk33e`) を送る。
- ルーム一覧処理: `Majak4_legacy/client/client/HgChnlM/HgChannelWnd.cpp::SetRoomInfo`
  - `G::keyOpMemberCnt` / `G::keyOpMemberId{i}` / `G::keyOpMemberPos{i}` を読み、切断中プレイヤー座席を検出する。
  - 自分の memberId が op member に含まれ、復帰待ち状態 (`keyRoomPlaying == 3`) なら `m_bContinuPlay = TRUE` とし、復帰対象 roomId を保持する。
- チャンネル入場完了後: `CHgChannelWnd::CompleteJoinChannel`
  - `m_bContinuPlay` が true なら `SendGameJoinRoom(m_nCpRoomId, "")` を自動送信する。
- サーバー復帰入室: `Majak4_legacy/server/server/HMajRoomServer.cpp::AutoJoinRoom`
  - `IsContinuePlayer(memberId)` → `FindContinuePlayer(memberId)` で元座席を特定する。
  - `RemoveContinuePlayer(pos)`、`ClearOutPlayer(memberId)` を行い、元座席に `AddPlayer()` する。
  - 全員復帰したら room state を `ReEnter()` で通常進行へ戻す。

移植ルール:

- Web クライアントはルーム接続断時に、接続が切れている画面から新しい操作を送ってはならない。インライン卓表示中は `RoomScreen` が即座にロビーへ強制遷移せず、`GameScene` / SignalR の reconnect・resync に復旧を委ねる。復旧できずユーザーが退室を選んだ場合だけロビーへ戻る。
- サーバー側では対局中の異常切断を完全退室にしてはならない。`IsOutPlayer=true` の continue player として元座席を保持し、`PlayerSessionService.DisconnectFromRoom` で古い connection/member mapping だけを外す。
- `mjkroom` / `c12e` の room info には active player と continue player を分けて含める。
  - active player: `keyMemberCnt`, `keyMemberId{i}`, `keyMemberPos{i}`
  - continue player: `keyOpMemberCnt`, `keyOpMemberId{i}`, `keyOpMemberPos{i}`
- ロビー/チャンネル再入場後、クライアントは room info の continue player に自分の `pix` があるか確認する。
- 自分が continue player なら、ユーザー操作を待たずに該当 roomId へ `mjkc6e` (`commandMajAutoEnterRoom`) / GameJoin 相当で自動復帰する。
- 自分の continue room が生存している間、別ルームの作成・通常入室・観戦入室を許可してはならない。サーバーはJWT由来の `continue:{memberNo}:room` とroom infoを確認し、要求roomIdがcontinue roomIdと異なる場合は拒否する。
- 復帰入室では新規空席割り当てをしてはならない。必ず元座席へ戻す。
- 同一内部 `member_no` がactive playerとして既にいる場合はduplicateとして扱うが、`IsOutPlayer` / continue playerの同一人物は復帰対象として扱う。
- ユーザーが接続断後に明示的に「退室」を選び、`c9e` を送れなかった場合、Webクライアントは次の `c1e` に `abandonPreviousRoom=true` と `abandonRoomId` を付ける。サーバーは該当roomが同一channelのPlaying roomでJWT本人の座席を持つ場合だけ古い接続を `IsOutPlayer` として切り離し、ロビー入場を `USER_MULTI_LOGIN` で拒否してはならない。
- 古いconnectionの `OnDisconnectedAsync` / `Remove(connectionId)` が遅れて到着しても、新しいconnectionの `member_no -> connectionId` mappingを消してはならない。削除はconnectionIdが現在のmappingと一致する場合だけ行う。
- `keyOpMember*` を通常メンバー数やルーム満員判定から消してはならない。レガシーでは落ち戻り座席としてルーム表示・復帰判定に使う。
- このフローは WebSocket/SignalR の自動再接続とは別物である。接続が復旧しても、レガシー互換の復帰は room info と `mjkc6e` によって成立する。

---

## 主要キー一覧

### 戦績系 (`mjkk1e`〜`mjkk18e`)

| キー | コード | 意味 |
|---|---|---|
| `keyTurnCnt` | `mjkk1e` | 総ターン数 |
| `keyDaidaCnt` | `mjkk2e` | 大台数 |
| `keyPointSum` | `mjkk3e` | 総点数 |
| `keyKyokuCnt` | `mjkk4e` | 局数 |
| `keyHoraCnt` | `mjkk5e` | 和了回数 |
| `keyHoraPoint` | `mjkk6e` | 和了点数合計 |
| `keyHojuCnt` | `mjkk7e` | 放銃回数 |
| `keyHojuPoint` | `mjkk8e` | 放銃点数合計 |
| `keyRichiCnt` | `mjkk9e` | リーチ回数 |
| `keyFuroCnt` | `mjkk10e` | 副露回数 |
| `keyTipPoint` | `mjkk11e` | チップ点数 |
| `keyTipMatchCnt` | `mjkk12e` | チップ対象局数 |
| `keyTobiCnt` | `mjkk13e` | 飛び回数 |
| `keyTobashiCnt` | `mjkk14e` | 飛ばし回数 |
| `keyDoraCnt` | `mjkk15e` | ドラ枚数 |
| `keyUraDoraCnt` | `mjkk16e` | 裏ドラ枚数 |
| `keyNukiDoraCnt` | `mjkk17e` | 抜きドラ枚数 |
| `keyRichiHoraCnt` | `mjkk18e` | リーチ和了回数 |

### プレイヤー情報系 (`mjkk28e`〜`mjkk52e`)

| キー | コード | 意味 |
|---|---|---|
| `keyAIKind` | `mjkk28e` | AI種別 |
| `keyServerTime` | `mjkk32e` | サーバー時刻 |
| `keyIsContinue` | `mjkk33e` | 継続プレイフラグ |
| `keyNickName` | `mjkk34e` | ニックネーム |
| `keyCupPoint` | `mjkk35e` | カップポイント |
| `keyExperience` | `mjkk36e` | 経験値 |
| `keyMemorialShop` | `mjkk37e` | 記念ショップ |
| `keyChangBestLevel` | `mjkk38e` | 最高レベル |
| `keySkinDataCount` | `mjkk39e` | スキンデータ数 |
| `keySkinInfo` | `mjkk40e` | スキン情報 |
| `keyLentMoney` | `mjkk41e` | 借りコイン |
| `keyReplenishmentType` | `mjkk42e` | 補充タイプ |
| `keyRestAllInCnt` | `mjkk43e` | 残オールイン回数 |
| `keyAllInCnt` | `mjkk44e` | オールイン回数 |
| `keyUseLentMoney` | `mjkk45e` | 借りコイン使用フラグ |
| `keyTrickTitle` | `mjkk46e` | トリック称号 |
| `keyMajakTitle` | `mjkk47e` | 麻雀称号 |
| `keyTitleType` | `mjkk48e` | 称号タイプ |
| `keyTitleCode` | `mjkk49e` | 称号コード |
| `keyTitleName` | `mjkk50e` | 称号名 |
| `keyTrickTitleName` | `mjkk51e` | トリック称号名 |
| `keyMajakTitleName` | `mjkk52e` | 麻雀称号名 |

### アイテム/ジェム系 (`mjkk53e`〜`mjkk64e`)

| キー | コード | 意味 |
|---|---|---|
| `keyDate` | `mjkk53e` | 日付 |
| `keyRichiEffect` | `mjkk54e` | リーチエフェクト |
| `keyGemCount` | `mjkk55e` | 龍宝石所持数 |
| `keyGemGame` | `mjkk56e` | 龍宝石ゲーム獲得数 |
| `keySellCode` | `mjkk57e` | 販売コード |
| `keyItemCode` | `mjkk58e` | アイテムコード |
| `keyBuyDate` | `mjkk59e` | 購入日 |
| `keyEndDate` | `mjkk60e` | 有効期限 |
| `keyUseFlag` | `mjkk61e` | 使用フラグ |
| `keyYakuName` | `mjkk62e` | 役名 |
| `keyEmoticonId` | `mjkk63e` | エモートID |
| `keyEmoticonAvatarId` | `mjkk64e` | エモートアバターID |

### グレードランキング系 (`mjkk65e`〜`mjkk81e`)

| キー | コード | 意味 |
|---|---|---|
| `keyGradeGetPoint` | `mjkk65e` | グレード獲得ポイント |
| `keyGradeCurrPoint` | `mjkk66e` | 現在グレードポイント |
| `keyGradeNextPoint` | `mjkk67e` | 次グレードポイント |
| `keyGradeGetRating` | `mjkk68e` | グレード獲得レーティング |
| `keyGradePrevLevel` | `mjkk69e` | 前グレードレベル |
| `keyGradeCurrLevel` | `mjkk70e` | 現在グレードレベル |
| `keyGradeUpDown` | `mjkk71e` | グレード昇降フラグ |
| `keyGradeBeginner` | `mjkk72e` | 初心者フラグ |
| `keyGradeRankId` | `mjkk73e` | ランクID |
| `keyGradeRankDate` | `mjkk74e` | ランク日付 |
| `keyGradeRankReflesh` | `mjkk75e` | ランク更新フラグ |
| `keyGradeRankList` | `mjkk76e` | ランキングリスト |
| `keyGradeRankCnt` | `mjkk77e` | ランキング件数 |
| `keyGradeRankSelf` | `mjkk78e` | 自己ランク情報 |
| `keyGradeSelectList` | `mjkk79e` | グレード選択リスト |
| `keyGradeSelectCnt` | `mjkk80e` | グレード選択件数 |
| `keyGradeExtraStage` | `mjkk81e` | エクストラステージフラグ |

### トーナメント系 (`mjkk82e`〜`mjkk103e`)

| キー | コード | 意味 |
|---|---|---|
| `keyTournamentList` | `mjkk82e` | トーナメント一覧 |
| `keyTournamentCnt` | `mjkk83e` | トーナメント件数 |
| `keyTournamentBaseRule` | `mjkk84e` | 基本ルール |
| `keyTournamentMoneyRule` | `mjkk85e` | 金銭ルール |
| `keyTournamentName` | `mjkk86e` | トーナメント名 |
| `keyTournamentDate` | `mjkk87e` | 開催日時 |
| `keyTournamentNo` | `mjkk88e` | トーナメントNo |
| `keyTournamentDetail` | `mjkk89e` | トーナメント詳細 |
| `keyTournamentDetailCnt` | `mjkk90e` | 詳細件数 |
| `keyTournamentJoinChk` | `mjkk91e` | 参加確認フラグ |
| `keyDeliveryMessage` | `mjkk92e` | 配信メッセージ |
| `keyTournamentRegistDayTime` | `mjkk93e` | 登録日時 |
| `keyTournamentRegistFlag` | `mjkk94e` | 登録フラグ |
| `keyFailCode` | `mjkk95e` | 失敗コード |
| `keyFailCodeCnt` | `mjkk96e` | 失敗コード数 |
| `keyTournamentTotalReport` | `mjkk97e` | 総合レポート |
| `keyTournamentTotalReportCnt` | `mjkk98e` | 総合レポート件数 |
| `keyTournamentSubId` | `mjkk99e` | サブID |
| `keyRoomForceExitReason` | `mjkk100e` | 強制退室理由 |
| `keyTournamentChkRoomMember` | `mjkk101e` | ルームメンバー確認 |
| `keyTournamentRoomId` | `mjkk102e` | トーナメントルームID |
| `keyTournamentRoomOrder` | `mjkk103e` | ルーム順序 |

### ミッション/週間報酬系 (`mjkk105e`〜`mjkk128e`)

| キー | コード | 意味 |
|---|---|---|
| `keyPointDayOwn` | `mjkk105e` | 本日獲得済みポイント |
| `keyPointDayMax` | `mjkk106e` | 本日最大ポイント |
| `keyPointWeekOwn` | `mjkk107e` | 週間獲得済みポイント |
| `keyPointWeekMax` | `mjkk108e` | 週間最大ポイント |
| `keyDailyMission1`〜`keyDailyMission11` | `mjkk109e`〜`mjkk119e` | デイリーミッション (ログイン/プレイ×3/その他) |
| `keyWeeklyReward1`〜`keyWeeklyReward8` | `mjkk120e`〜`mjkk127e` | 週間報酬 |
| `keyWeeklyRewardId` | `mjkk128e` | 週間報酬ID |
| `keySerialCode` | `mjkk130e` | シリアルコード |

### カスタムアイテム系 (`mjkk134e`〜`mjkk140e`)

| キー | コード | 意味 |
|---|---|---|
| `keyCustomBoard` | `mjkk134e` | 背景板ID |
| `keyCustomHai` | `mjkk135e` | 牌デザインID |
| `keyCustomCostume` | `mjkk136e` | コスチュームID |
| `keyCustomCostumeType` | `mjkk137e` | コスチュームタイプ |
| `keyCustomId` | `mjkk138e` | カスタムID (汎用) |
| `keyShopNo` | `mjkk139e` | ショップNo (汎用) |
| `keyItemQuantity` | `mjkk140e` | アイテム数量 |

### ルーム/スコア系 (`smmk1e`〜`smmk14e`)

| キー | コード | 意味 |
|---|---|---|
| `keyRoomCharge` | `smmk1e` | 場代 |
| `keyOkButton` | `smmk2e` | OKボタン状態 |
| `keyLackMoney` | `smmk3e` | コイン不足フラグ |
| `keyWinMoneyCut` | `smmk4e` | 勝利コインカット |
| `keyScore` | `smmk5e` | スコア |
| `keyPoint` | `smmk6e` | ポイント (点棒) |
| `keyYakitori` | `smmk7e` | ヤキトリ |
| `keyChip` | `smmk8e` | チップ |
| `keyGateway` | `smmk9e` | ゲートウェイ |
| `keyMACAddr` | `smmk10e` | MACアドレス |
| `keyFeeWinner` | `smmk11e` | 勝者手数料 |
| `keyGetEventPoint` | `smmk11e` | イベントポイント獲得 |
| `keyCutEventPoint` | `smmk12e` | イベントポイント消費 |
| `keyTodayEventPoint` | `smmk13e` | 本日のイベントポイント |
| `keyTotalEventPoint` | `smmk14e` | 総イベントポイント |

---

## ゲーム内アクション列挙 (ACT enum / MajakDef.h)

| 値 | 名称 | 意味 |
|---|---|---|
| `INV` | Invalid | 無効 |
| `PAS` | Pass | パス |
| `CHI` | Chi | チー |
| `PON` | Pon | ポン |
| `KAN` | Kan | カン |
| `RON` | Ron | ロン |
| `TAP` | Tsumo+Agari | ツモ和了 |
| `ANK` | Ankan | 暗槓 |
| `CHA` | Chakan | 加槓 |
| `RIC` | Richi | リーチ |
| `TAO` | Taopai | 倒牌 |
| `TSU` | Tsumo | ツモ (打牌前) |
| `HUA` | Hua | 花牌 (三人麻雀用) |
| `SHU` | Shu | — |
| `KOU` | Kou | — |
| `LBU` | LBU | — |

---

## チャンネルタイプ一覧 (HMajChnlInfo)

| 定数 | 値 | 説明 |
|---|---|---|
| `CT_NORMAL` | 0 | 通常チャンネル |
| `CT_FRIEND` | 1 | フレンドチャンネル |
| `CT_MATCH` | 2 | マッチングチャンネル |
| `CT_RANKING` | 3 | ランキングチャンネル |
| `CT_GRADE` | 101 | グレードモードチャンネル |
| `CT_TOURNAMENT` | 102 | トーナメントチャンネル |
| `CT_HGDP` | 6 | ハンゲdeポンチャンネル |
| `CT_ARIARI` | 7 | アリアリチャンネル |
| `CT_TRAINING` | 8 | 練習チャンネル |
| `CT_CUP` | 100 | カップ/イベントチャンネル |
| `CT_CIRCLE` | 103 | サークルチャンネル |

オートマッチングはサブID の 2 文字目が `Z` の場合に有効 (`m_szSubId[1]=='Z'`)。
初心者チャンネルはサブID の 1 文字目が `1` (`m_szSubId[0]=='1'`)。

---

## 定数値 (valuePlayer / valueViewer)

| 定数 | 値 | 意味 |
|---|---|---|
| `valuePlayer` | `1` | プレイヤーとして参加 |
| `valueViewer` | `2` | 観戦者として参加 |
| `valueRankDuring` / `valueDuring` | `mjkv1e` | ランキング集計中 |
| `valueRankNoData` / `valueNoData` | `mjkv2e` | ランキングデータなし |
| `valueCustomSuccess` | `0` | カスタム処理成功 |
| `valueCustomCoinless` | `1` | コイン不足 |
| `valueCustomOwned` | `2` | 既所持 |
| `valueCustomIDError` | `11` | IDエラー |
| `valueCustomDBError` | `12` | DBエラー |
| `valueCustomERROR` | `13` | 汎用エラー |

---

## 結果値/戻り値規約

- 実装関数は基本 `BOOL` を返却。
  - `TRUE`: 正常処理継続
  - `FALSE`: 異常または切断誘導
- クライアント向け成否は `G::keyResult` で返す。
  - 典型値: `G::valueSuccess`, `G::valueFailure`
- カスタムアイテム系は `valueCustom*` 定数で詳細エラーを返す。

---

## ゲーム定数 (MajakDef.h)

| 定数 | 値 | 意味 |
|---|---|---|
| `PLAYER_MAX_COUNT` | `4` | 最大プレイヤー数 |
| `BIPAI_MAX_COUNT` | `136` | 総牌数 |
| `WANPAI_COUNT` | `14` | 王牌数 |
| `TEHAI_COUNT` | `13` | 手牌数 |
| `DORA_MAX_COUNT` | `5` | ドラ最大枚数 |
| `HANCHAN_LASTKYOKU` | `7` (4×2-1) | 半荘最終局 |
| `TONPU_LASTKYOKU` | `3` (4×1-1) | 東風最終局 |
| `KAESHIPOINT` | `30000` | 返し点 |
| `DEFAULT_GAMEPOINT` | `25000` | 持ち点初期値 |
| `DEFAULT_TIP` | `20` | チップ初期値 |
| `DEFAULT_MONEY` | `1000` | デフォルトコイン |
| `ALLINCOUNT_MAX` | `1` | オールイン最大回数 (通常) |
| `YAKUMANBONUS_MONEY` | `200` | 役満ボーナスコイン |

## ASP.NET Core/.NET 8移植メモ

### プロトコル実装仕様
- プロトコル形状：「service + command + key-value」
- SignalRは `/hubs/majak` を使用し、接続時にゲームJWTを必須とする。
- 本人確認はJWTから設定された `CommandContext.AuthMemberNo` / `AuthPix` を正本とし、ペイロードのIDだけを信頼しない。
- レガシー互換を維持するキーは麻雀4の `mjkk*e` / `smmk*e`、コマンドは `mjkc*e` / `smmc*e`、値は `mjkv*e` 系である。
- 新しいWeb専用機能は認証済みREST APIを追加できるが、既存の獲得通知や対局コマンドを別用途へ転用しない。
