---
applyTo: "server/**,server.tests/**,client/**,scripts/**"
description: "麻雀4のレーティング、GP資産レベル、段位、段位戦卓の入場条件を確認・変更するときに参照する"
---

# AP-07 レーティング / レベル制限ルール

本ドキュメントは麻雀4のレーティング、GP資産レベル、段位、段位戦卓の入場条件を定義する。
ゲーム仕様はAP-15、通貨名称はAP-16を優先し、実装値は `RatingService` と `GradeLevelTable` を同時に確認する。

---

## 1. 3つの値を混同しない

| 値 | 意味 | 主な保存先 | 判定 |
|---|---|---|---|
| `Rating` | 対局モードごとの実力レーティング | `player_mode_stats.rating` | 対局結果とモード別計算 |
| `NLevel` / `SLevel` | 所持GPに基づく資産レベル・表示名 | `player_profile` と実行時 `MajakPlayer` | `RatingService.GetNLevel` / `GetSLevel` |
| `GradeLevel` / `GradePoint` | 段位戦の10級〜九段と昇段ポイント | `player_mode_stats` のgradeモード | `GradeLevelTable` と段位戦結果 |

- レーティング、資産レベル、段位は別概念であり、相互の代替値として使わない。
- `SLevel` は段位名ではなく、GP所持額による資産称号である。
- ロビーや対局情報に `rating`、`nlevel`、`slevel` を送る場合は、DBまたはサーバー計算値を使い、クライアントで仮の固定値を作らない。

---

## 2. GP資産レベル (`NLevel` / `SLevel`)

`RatingService` は現在の `GamMoney` から次の11段階を計算する。

| NLevel | SLevel | GP下限 |
|---:|---|---:|
| 0 | 無一文 | 0 |
| 1 | 金欠 | 1 |
| 2 | 庶民 | 500 |
| 3 | 平民 | 1,500 |
| 4 | 一般人 | 3,000 |
| 5 | 中流 | 10,000 |
| 6 | 上流 | 30,000 |
| 7 | 金持ち | 100,000 |
| 8 | 富豪 | 500,000 |
| 9 | 大富豪 | 1,000,000 |
| 10 | 財閥 | 5,000,000 |

- `UpdatePlayerLevel` は現在のGPから常に再計算するため、GPが減れば実行時レベルも下がり得る。
- GPの増減、表示、履歴、無料補充はAP-16に従う。
- 資産レベルを新規機能の利用権限に転用する場合は、公式仕様または明示的な要件が必要である。

---

## 3. 段位と昇段ポイント

- `GradeLevel` は `0=10級` から `9=1級`、`10=初段` から `18=九段` とする。
- `GradeLevelTable.GetMaxPoint` の昇段ポイントは次の順である。
  - 10級〜7級: 30
  - 6級〜4級: 60
  - 3級〜1級: 90
  - 初段: 600
  - 二段〜三段: 1,200
  - 四段〜六段: 2,400
  - 七段〜九段: 4,800
- gradeモードの初期レーティングは `GameConst.RatingGradeModeInit=1500` とする。
- 段位名、必要ポイント、レーティング更新をクライアントだけで確定せず、サーバー応答を正本とする。

---

## 4. 段位戦卓の入場条件

`RatingService.CheckEnterGradeMode(gradeLevel, gamMoney, subId)` をサーバー側の正本とする。

| SubID末尾 | 卓 | GradeLevel | GP下限 |
|---|---|---:|---:|
| `A` | 通常卓 | 0〜12 (10級〜三段) | 500 |
| `B` | 段位卓 | 10〜18 (初段〜九段) | 5,000 |
| `C` | 高段位卓 | 13〜18 (四段〜九段) | 10,000 |
| `D` | 十段位卓 | 16〜18 (七段〜九段) | 30,000 |

- 実際の対局開始同意時にもサーバーが条件を再検証する。
- クライアントのボタン非活性化や説明は補助表示であり、権限制御の代わりにしない。
- 初心者卓は別条件として通常戦績の対局数とGPを確認する。段位戦条件へ混ぜない。
- 公式仕様にない「レベルXでショップ解放」等の独自制限を追加しない。

## 5. 関連実装

- `server/Services/RatingService.cs`: GP資産レベル、gradeレーティング、段位戦入場条件
- `server/Models/Protocol/GradeLevelTable.cs`: 段位ごとの最大ポイント
- `server/Models/Protocol/GameConst.cs`: grade初期値と共通定数
- `server/Commands/Room/RoomCommands.cs`: 対局開始時の最終入場条件確認
- `server/Repositories/MySQL/PlayerRepository.cs`: mode別レーティング・段位データの永続化
- `server.tests/RatingServiceTests.cs`: GP境界と段位戦卓境界の回帰テスト
