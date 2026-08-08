---
applyTo: "server/**,scripts/**"
description: "MySQLゲームDB・ログDBのアクセス境界、Repository責務、接続規約"
---

# AP-10 データベースアクセス規約

## 1. 現行構成

現行サーバーの永続化先はMySQLのみである。Oracleドライバー、Oracle接続、Oracle用Repository、
ストアドプロシージャ呼び出しは使用しない。レガシーOracle SQLとプロシージャは移植仕様の確認に
のみ使用し、実行経路へ戻してはならない。

| 項目 | 現行実装 |
|---|---|
| DBエンジン | MySQL 8.0以上 |
| EF Coreプロバイダー | Pomelo.EntityFrameworkCore.MySql |
| 直接接続 | MySqlConnector |
| ゲームDB | `majak_game` |
| ログDB | `majak_log` |
| ゲームEFコンテキスト | `GameDataContext` |
| ログEFコンテキスト | `LogDataContext` |
| DI登録 | `Program.cs` のMySQL Factory / Repository登録 |

スキーマと初期データの正本はAP-03および次の4ファイルとする。

- `scripts/database/game/001_create_tables.sql`
- `scripts/database/game/002_seed_data.sql`
- `scripts/database/log/001_create_tables.sql`
- `scripts/database/log/002_seed_data.sql`

## 2. DB境界

### 2-1. ゲームDB (`majak_game`)

プレイヤーの現在状態、所持品、マスター、チャンネル、イベント、カップ、トーナメントを保持する。
更新後の状態を再読込する必要があるデータ、同一処理内で整合性を守る必要があるデータはゲームDBへ置く。

主なデータ:

- プレイヤーアカウント、ウォレット、プロフィール、モード別戦績
- 称号、機能アイテム、カスタムアイテム、スキン、ショップ、プレゼント
- ミッション、週間報酬、段位、役別統計
- チャンネル、ルール、商品、取引コード、イベント、カップ、トーナメント

### 2-2. ログDB (`majak_log`)

追記型の対局結果、取引、監査、配布・受取履歴を保持する。ゲームDBの現在状態をログDBから
復元する設計にしてはならない。

主なデータ:

- 対局ヘッダとプレイヤー別対局結果
- 練習対局ヘッダとプレイヤー別結果
- ゲームマネー変動、アイテム購入、GEM取引
- 役履歴、週間報酬、デイリーミッション、プレゼント配布
- 段位スナップショット、カップ対局、管理操作、ログイン

ゲームDBとログDBの間に外部キーや分散トランザクションを作成しない。ゲーム状態の更新を先に確定し、
必要なログを明示的に追記する。ログ書込み失敗時の扱いは呼び出し元のレガシー仕様に合わせる。

## 3. 接続生成

| 用途 | 接続情報 | Factory | EFコンテキスト |
|---|---|---|---|
| ゲームDB | `GameDbContext` | `GameDataContextFactory` | `GameDataContext` |
| ログDB | `LogDbContext` | `LogDataContextFactory` | `LogDataContext` |

- Factoryは処理単位でEFコンテキストを生成し、呼び出し元は `await using` で破棄する。
- PomeloはMySQL 8.0として構成し、一時的な接続障害には `EnableRetryOnFailure()` を使用する。
- ゲームDBとログDBは別の接続プールとして扱い、Factoryやコンテキストを流用しない。
- 接続文字列は初回要求時に解決してキャッシュする。アプリ起動だけでDBやParameter Storeへ接続しない。
- `ConnectionStrings:GameDatabase` と `ConnectionStrings:LogDatabase` をローカル設定のフォールバックとする。
- 接続文字列、ユーザー名、パスワードをログへ出力しない。

## 4. Repository責務

| Repository | DB | 責務 |
|---|---|---|
| `PlayerRepository` | ゲーム | プレイヤー状態、戦績、ウォレット、称号、ミッション、報酬 |
| `GamePlayerRepository` | ゲーム | 対局中プレイヤー状態の読込・更新 |
| `ItemRepository` | ゲーム / ログ | 商品・所持品・購入状態の更新と購入ログ連携 |
| `TournamentRepository` | ゲーム | カップ・トーナメント・段位運用データ |
| `ChannelRepository` | ゲーム | チャンネル設定とランタイム情報 |
| `AdminRepository` | ゲーム | 管理者、承認、停止、GEM商品管理 |
| `HistoryRepository` | ゲーム / ログ | レガシー履歴APIを現行ログ書込みへ橋渡し |
| `LogRepository` | ログ | 追記型ログの実書込み |

旧 `StoredProcedureRepository` の責務は各ドメインRepositoryへ分解済みである。新しいDB処理を
汎用プロシージャRepositoryへ集約せず、状態を所有するRepositoryへ追加する。

## 5. 取引コードとゲームマネー履歴

レガシー `PROCODET` の現行テーブルは `transaction_code_master` である。ゲームマネー履歴では
`TransactionCodeMetadataResolver` が取引コードから次を解決する。

| マスター項目 | ログ項目 | 動作 |
|---|---|---|
| `code_title` | `event_title` | NULL時は取引コードを使用 |
| `game_id` | `game_id` | NULL時は `MAJAK4` を使用 |
| `is_history_enabled` | `is_valid` | falseでも行を抑止せず無効フラグとして保存 |

- 取引コードがマスターに存在しない場合、履歴行を作成しない。
- 存在するコードは `is_history_enabled = false` でも履歴行を作成する。
- タイトルやゲームIDを呼び出し元で推測・固定せず、必ずマスター値を使用する。

## 6. トランザクション規約

- 同一DB内で複数テーブルを一体更新する処理はEF Coreトランザクションで囲む。
- 対局ヘッダとプレイヤー別結果、練習ヘッダとプレイヤー別結果は同一ログDBトランザクションで保存する。
- ゲームDB更新とログDB追記を単一トランザクションとして扱わない。
- `SaveChangesAsync()` の途中で例外が発生した場合、開始したトランザクションをロールバックして再送出する。
- 数値の縮小変換は `checked` を使用し、桁あふれを暗黙に切り捨てない。

## 7. 実装規約

- SQL識別子はAP-03の `snake_case` 名を使用する。
- Oracleの大文字テーブル名、`:parameter`、`MERGE INTO`、`DUAL`、`NVL`、`SYSDATE` を新規コードへ持ち込まない。
- パラメーターを文字列連結したSQLを作らない。EF Core式またはMySqlConnectorのパラメーターを使用する。
- 読取り専用クエリでは、追跡が不要なら `AsNoTracking()` を使用する。
- ユーザー状態を更新する前に、レガシーの成功条件、NULLと空文字、更新順、エラーコードを確認する。
- テーブル、カラム、キー、インデックスを変更したら、4つの基準SQLとAP-03を同時に更新する。
- ログテーブルに外部キーを追加しない。ゲームDBの主キー存在をログDB書込み時に照会しない。

## 8. レガシー参照の扱い

`Majak4_legacy/server/server/HMajDBObject.cpp`、`HMajLogDBObject.cpp`、旧プロシージャ定義は挙動確認用の
資料である。移植時は入力、出力、条件分岐、更新順、履歴項目を確認し、現行のMySQLテーブルと
Repositoryへ対応付ける。レガシーDB名やプラットフォーム基盤テーブルを現行ランタイム依存として
再導入してはならない。