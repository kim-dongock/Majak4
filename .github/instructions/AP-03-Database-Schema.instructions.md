---
applyTo: "server/Repositories/MySQL/**,scripts/database/**"
description: "使用条件: MySQLゲーム・ログDBのテーブル、カラム、キー、インデックス、初期データ、または作成スクリプトを確認・変更するとき"
---

# AP-03 MySQLデータベーススキーマ

## 1. 正本と適用原則

- 運用データベースは `majak_game` と `majak_log` に物理分離する。
- 新規データベースでは各DBの `001_create_tables.sql` の後に `002_seed_data.sql` を実行する。
- 4ファイルは新規構築用の最終基準であり、既存運用DBへ再適用しない。
- ゲームDB・ログDBともに外部キー制約を作成しない。
- MySQL 8.0以上、`utf8mb4`、`utf8mb4_0900_ai_ci` を基準とする。

| DB | テーブル作成 | 初期データ | テーブル数 |
|---|---|---|---:|
| majak_game | scripts/database/game/001_create_tables.sql | scripts/database/game/002_seed_data.sql | 52 |
| majak_log | scripts/database/log/001_create_tables.sql | scripts/database/log/002_seed_data.sql | 16 |

## 2. 表記規則

- カラム定義は基準SQLの型、NULL、デフォルト、自動採番、自動更新式をそのまま記載する。
- 複合主キー、UNIQUE、INDEX、CHECKは各テーブルの制約欄に記載する。
- `DATETIME(3)` はミリ秒精度とし、アプリケーションではUTCとして扱う。
- `BOOLEAN` はMySQLの `TINYINT(1)` の別名である。

## 3. ゲームDBテーブル一覧 (`majak_game`)

| テーブル | カラム数 |
|---|---:|
| `player_account` | 16 |
| `player_wallet` | 9 |
| `player_profile` | 17 |
| `player_mode_stats` | 37 |
| `player_high_class_summary` | 10 |
| `player_high_class_yaku` | 4 |
| `gem_product_master` | 10 |
| `gem_charge_order` | 12 |
| `gem_item_price` | 9 |
| `admin_account` | 6 |
| `transaction_code_master` | 16 |
| `channel_master` | 13 |
| `rule_master` | 8 |
| `title_master` | 6 |
| `daily_mission_master` | 4 |
| `weekly_reward_master` | 4 |
| `function_item_master` | 5 |
| `billing_item_master` | 28 |
| `custom_item_master` | 6 |
| `custom_item_set` | 4 |
| `custom_shop_master` | 12 |
| `cup_master` | 14 |
| `cup_channel` | 2 |
| `tournament_plan` | 22 |
| `tournament_limit` | 6 |
| `grade_rank_schedule` | 4 |
| `player_daily_mission` | 6 |
| `player_weekly_reward` | 6 |
| `player_title` | 4 |
| `player_function_item` | 8 |
| `player_custom_item` | 6 |
| `player_present` | 11 |
| `player_grade_rank` | 13 |
| `player_yaku_stats` | 54 |
| `cup_player_rating` | 6 |
| `tournament_player_rating` | 16 |
| `tournament_session` | 33 |
| `tournament_participant` | 10 |
| `tournament_room` | 33 |
| `channel_runtime` | 29 |
| `event_master` | 10 |
| `event_user` | 19 |
| `game_admin_member` | 5 |
| `player_avatar_inventory` | 6 |
| `player_daily_mission_history` | 7 |
| `player_skin` | 6 |
| `player_shop` | 4 |
| `memorial_shop_master` | 4 |
| `event_gift_master` | 15 |
| `serial_exchange_item` | 8 |
| `serial_coupon` | 11 |
| `game_clear_count` | 10 |

## 4. ログDBテーブル一覧 (`majak_log`)

| テーブル | カラム数 |
|---|---:|
| `game_session_log` | 16 |
| `game_player_result_log` | 24 |
| `training_session_log` | 6 |
| `training_player_result_log` | 5 |
| `weekly_reward_claim_log` | 6 |
| `money_transaction_log` | 15 |
| `winning_yaku_log` | 5 |
| `item_purchase_log` | 9 |
| `gem_transaction_log` | 11 |
| `admin_operation_log` | 11 |
| `player_login_log` | 6 |
| `daily_mission_completion_log` | 7 |
| `custom_item_purchase_log` | 10 |
| `present_delivery_log` | 9 |
| `grade_rank_snapshot_log` | 8 |
| `cup_match_log` | 8 |

## 5. 初期データ

`majak_game` にはマスター・設定・商品データのみを投入し、ユーザー状態や履歴データは投入しない。

| テーブル | 初期行数 |
|---|---:|
| `gem_product_master` | 15 |
| `transaction_code_master` | 233 |
| `channel_master` | 20 |
| `rule_master` | 25 |
| `title_master` | 150 |
| `daily_mission_master` | 11 |
| `weekly_reward_master` | 8 |
| `function_item_master` | 7 |
| `billing_item_master` | 18 |
| `custom_item_master` | 42 |
| `custom_item_set` | 20 |
| `custom_shop_master` | 31 |
| `cup_master` | 7 |
| `cup_channel` | 2887 |
| `tournament_plan` | 60 |
| `tournament_limit` | 4 |
| `game_clear_count` | 1 |
| **合計** | **3539** |

- `CHANELWT.csv` は運用時ランタイムスナップショットのため投入しない。
- 旧プラットフォーム名を含むURL・文字列は空文字へ置換する。
- `majak_log` に初期データはない。

## 6. ゲームDBカラム定義

### `player_account`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `display_name` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `email` | `VARCHAR(254) NULL` |
| `google_sub` | `VARCHAR(64) NULL` |
| `sex_code` | `CHAR(1) NOT NULL DEFAULT 'U'` |
| `avatar_id` | `VARCHAR(255) NOT NULL DEFAULT ''` |
| `terms_agreed_at` | `DATETIME(3) NULL` |
| `approved_at` | `DATETIME(3) NULL` |
| `approved_by` | `BIGINT UNSIGNED NULL` |
| `reject_reason` | `VARCHAR(200) NULL` |
| `account_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `source_environment` | `VARCHAR(16) NOT NULL DEFAULT 'production'` |
| `first_login_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `last_login_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`
- `UNIQUE KEY idx_player_account_google_sub (google_sub)`
- `INDEX idx_player_account_status_created (account_status, created_at)`
- `CONSTRAINT chk_player_account_sex CHECK (sex_code IN ('M', 'F', 'U'))`
- `CONSTRAINT chk_player_account_status CHECK (account_status IN (0, 1, 2))`

### `player_wallet`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `game_money` | `BIGINT NOT NULL DEFAULT 1000` |
| `pending_game_money` | `BIGINT NOT NULL DEFAULT 0` |
| `earned_game_money` | `BIGINT NOT NULL DEFAULT 0` |
| `loaned_game_money` | `BIGINT NOT NULL DEFAULT 0` |
| `gem_count` | `INT NOT NULL DEFAULT 0` |
| `row_version` | `BIGINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`
- `CONSTRAINT chk_player_wallet_game_money CHECK (game_money >= 0)`
- `CONSTRAINT chk_player_wallet_gem_count CHECK (gem_count >= 0)`

### `player_profile`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `common_rating` | `INT NOT NULL DEFAULT 1400` |
| `experience` | `INT NOT NULL DEFAULT 0` |
| `best_money_level` | `TINYINT UNSIGNED NOT NULL DEFAULT 2` |
| `consecutive_win_loss` | `INT NOT NULL DEFAULT 0` |
| `all_in_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `last_all_in_at` | `DATETIME(3) NULL` |
| `trick_title_code` | `VARCHAR(32) NULL` |
| `majak_title_code` | `VARCHAR(32) NULL` |
| `event_open_flag` | `CHAR(1) NULL` |
| `weekly_point` | `INT NOT NULL DEFAULT 0` |
| `weekly_target_date` | `DATE NOT NULL DEFAULT (CURRENT_DATE)` |
| `joined_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `last_played_at` | `DATETIME(3) NULL` |
| `row_version` | `BIGINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`
- `CONSTRAINT chk_player_profile_experience CHECK (experience >= 0)`
- `CONSTRAINT chk_player_profile_all_in_count CHECK (all_in_count >= 0)`

### `player_mode_stats`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `mode_code` | `VARCHAR(20) NOT NULL` |
| `rating` | `INT NOT NULL DEFAULT 1400` |
| `match_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `win_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `defeat_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `draw_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `first_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `second_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `third_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `fourth_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `turn_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `dealer_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `point_sum` | `BIGINT NOT NULL DEFAULT 0` |
| `round_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `win_hand_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `win_hand_points` | `BIGINT NOT NULL DEFAULT 0` |
| `deal_in_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `deal_in_points` | `BIGINT NOT NULL DEFAULT 0` |
| `riichi_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `meld_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `tip_point` | `BIGINT NOT NULL DEFAULT 0` |
| `tip_match_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `bust_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `bust_other_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `dora_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `ura_dora_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `riichi_win_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `disconnect_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `last_disconnect_at` | `DATETIME(3) NULL` |
| `last_channel_id` | `VARCHAR(30) NULL` |
| `grade_level` | `INT NOT NULL DEFAULT 0` |
| `grade_point` | `INT NOT NULL DEFAULT 0` |
| `extra_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `last_extra_at` | `DATETIME(3) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, mode_code)`
- `CONSTRAINT chk_player_mode_stats_mode CHECK (mode_code IN ('regular', 'compete', 'high_class', 'grade', 'agari', 'hgdp'))`

### `player_high_class_summary`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `score_max` | `INT NULL` |
| `score_min` | `INT NULL` |
| `money_max` | `BIGINT NULL` |
| `money_min` | `BIGINT NULL` |
| `win_hand_dora_max` | `INT NOT NULL DEFAULT 0` |
| `consecutive_top_max` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `consecutive_top_current` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`

### `player_high_class_yaku`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `yaku_id` | `SMALLINT UNSIGNED NOT NULL` |
| `count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, yaku_id)`

### `gem_product_master`

| カラム | 最終定義 |
|---|---|
| `product_id` | `VARCHAR(50) NOT NULL` |
| `display_name` | `VARCHAR(100) NOT NULL` |
| `gem_amount` | `INT UNSIGNED NOT NULL` |
| `price_jpy` | `INT UNSIGNED NOT NULL` |
| `platform` | `ENUM('web','ios','android','all') NOT NULL DEFAULT 'all'` |
| `store_product_id` | `VARCHAR(200) NULL` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `sort_order` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (product_id)`
- `CONSTRAINT chk_gem_product_gem_amount CHECK (gem_amount > 0)`
- `CONSTRAINT chk_gem_product_price_jpy CHECK (price_jpy > 0)`

### `gem_charge_order`

| カラム | 最終定義 |
|---|---|
| `order_id` | `VARCHAR(64) NOT NULL` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `product_id` | `VARCHAR(50) NOT NULL` |
| `platform` | `ENUM('web','ios','android') NOT NULL` |
| `gem_amount` | `INT UNSIGNED NOT NULL` |
| `price_jpy` | `INT UNSIGNED NOT NULL` |
| `status` | `ENUM('pending','completed','failed','refunded') NOT NULL DEFAULT 'pending'` |
| `pg_txn_id` | `VARCHAR(200) NULL` |
| `pg_raw_response` | `TEXT NULL` |
| `client_ip` | `VARCHAR(45) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `completed_at` | `DATETIME(3) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (order_id)`
- `INDEX idx_gem_order_member (member_no)`
- `INDEX idx_gem_order_status (status, created_at)`
- `INDEX idx_gem_order_pg_txn (pg_txn_id)`
- `CONSTRAINT chk_gem_order_gem_amount CHECK (gem_amount > 0)`
- `CONSTRAINT chk_gem_order_price_jpy CHECK (price_jpy > 0)`

### `gem_item_price`

| カラム | 最終定義 |
|---|---|
| `item_key` | `VARCHAR(60) NOT NULL` |
| `item_type` | `ENUM('custom_item','function_item') NOT NULL` |
| `gem_price` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `game_money_price` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `sale_start_at` | `DATETIME(3) NULL` |
| `sale_end_at` | `DATETIME(3) NULL` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (item_key, item_type)`
- `CONSTRAINT chk_gem_item_price_gem CHECK (gem_price >= 0)`
- `CONSTRAINT chk_gem_item_price_money CHECK (game_money_price >= 0)`

### `admin_account`

| カラム | 最終定義 |
|---|---|
| `admin_no` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `email` | `VARCHAR(200) NOT NULL` |
| `role` | `ENUM('super_admin','operator','viewer') NOT NULL DEFAULT 'operator'` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (admin_no)`
- `UNIQUE KEY uq_admin_account_email (email)`

### `transaction_code_master`

| カラム | 最終定義 |
|---|---|
| `transaction_code` | `VARCHAR(20) NOT NULL` |
| `code_title` | `VARCHAR(30) NULL` |
| `is_history_enabled` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `is_cumulative` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `open_status` | `CHAR(1) NULL` |
| `start_date` | `DATE NULL` |
| `content` | `VARCHAR(80) NULL` |
| `service_code` | `VARCHAR(10) NULL` |
| `service_name` | `VARCHAR(30) NULL` |
| `is_service_enabled` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `game_id` | `VARCHAR(10) NULL` |
| `registrant_name` | `VARCHAR(20) NULL` |
| `planner_name` | `VARCHAR(20) NULL` |
| `developer_name` | `VARCHAR(20) NULL` |
| `direction_code` | `CHAR(1) NULL` |
| `avatar_code` | `VARCHAR(10) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (transaction_code)`
- `INDEX idx_transaction_code_master_avatar (avatar_code)`
- `INDEX idx_transaction_code_master_game (game_id, is_history_enabled)`

### `channel_master`

| カラム | 最終定義 |
|---|---|
| `channel_id` | `VARCHAR(30) NOT NULL` |
| `game_id` | `VARCHAR(20) NOT NULL DEFAULT 'MAJAK4'` |
| `sub_id` | `VARCHAR(10) NOT NULL` |
| `channel_name` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `max_member` | `INT UNSIGNED NOT NULL DEFAULT 1024` |
| `max_room` | `INT UNSIGNED NOT NULL DEFAULT 256` |
| `unit_money` | `INT UNSIGNED NOT NULL DEFAULT 20` |
| `channel_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `env` | `VARCHAR(10) NOT NULL DEFAULT 'prod'` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `server_url` | `VARCHAR(255) NOT NULL DEFAULT ''` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (channel_id)`
- `INDEX idx_channel_master_subid (sub_id)`
- `INDEX idx_channel_master_gameid (game_id, env, is_active)`

### `rule_master`

| カラム | 最終定義 |
|---|---|
| `rule_id` | `SMALLINT UNSIGNED NOT NULL` |
| `judgement_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `room_option` | `CHAR(13) NOT NULL DEFAULT '0000000000000'` |
| `normal_yaku_condition` | `CHAR(28) NOT NULL DEFAULT '0000000000000000000000000000'` |
| `yakuman_condition` | `CHAR(15) NOT NULL DEFAULT '000000000000000'` |
| `rule_name` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `rule_detail` | `VARCHAR(2000) NOT NULL DEFAULT ''` |
| `evt_sum_type` | `INT UNSIGNED NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (rule_id)`

### `title_master`

| カラム | 最終定義 |
|---|---|
| `title_id` | `VARCHAR(10) NOT NULL` |
| `title_name` | `VARCHAR(30) NOT NULL DEFAULT ''` |
| `img_filename` | `VARCHAR(20) NOT NULL DEFAULT ''` |
| `attribute` | `CHAR(1) NOT NULL DEFAULT 'T'` |
| `title_rank` | `TINYINT UNSIGNED NOT NULL DEFAULT 1` |
| `description` | `VARCHAR(200) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (title_id)`

### `daily_mission_master`

| カラム | 最終定義 |
|---|---|
| `mission_id` | `TINYINT UNSIGNED NOT NULL` |
| `condition_type` | `TINYINT UNSIGNED NOT NULL` |
| `condition_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 1` |
| `point` | `TINYINT UNSIGNED NOT NULL DEFAULT 5` |

キー・インデックス・制約:

- `PRIMARY KEY (mission_id)`

### `weekly_reward_master`

| カラム | 最終定義 |
|---|---|
| `reward_id` | `TINYINT UNSIGNED NOT NULL` |
| `reward_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 1` |
| `reward_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `required_point` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |

キー・インデックス・制約:

- `PRIMARY KEY (reward_id)`
- `CONSTRAINT chk_weekly_reward_type CHECK (reward_type IN (1, 2))`

### `function_item_master`

| カラム | 最終定義 |
|---|---|
| `item_code` | `VARCHAR(10) NOT NULL` |
| `item_name` | `VARCHAR(24) NOT NULL DEFAULT ''` |
| `category_no` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `sub_no` | `SMALLINT UNSIGNED NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (item_code)`

### `billing_item_master`

| カラム | 最終定義 |
|---|---|
| `item_code` | `VARCHAR(5) NOT NULL` |
| `sub_code` | `VARCHAR(6) NOT NULL` |
| `item_name` | `VARCHAR(30) NOT NULL DEFAULT ''` |
| `item_type` | `CHAR(1) NULL` |
| `full_count` | `INT NULL` |
| `unit_money` | `INT UNSIGNED NULL` |
| `repay_amount` | `BIGINT NULL` |
| `internal_comment` | `VARCHAR(150) NULL` |
| `secondary_comment` | `VARCHAR(200) NULL` |
| `is_on_sale` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `is_usable` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `age_limit` | `SMALLINT UNSIGNED NULL` |
| `sex_code` | `CHAR(1) NULL` |
| `item_description` | `VARCHAR(300) NULL` |
| `give_resource` | `VARCHAR(20) NULL` |
| `give_money_type` | `CHAR(1) NULL` |
| `function_box` | `VARCHAR(200) NULL` |
| `is_client_only` | `BOOLEAN NULL` |
| `is_used_on_purchase` | `BOOLEAN NULL` |
| `max_purchase_count` | `SMALLINT UNSIGNED NULL` |
| `available_days` | `INT NULL` |
| `is_resellable` | `BOOLEAN NULL` |
| `is_presentable` | `BOOLEAN NULL` |
| `is_presentable_in_bag` | `BOOLEAN NULL` |
| `is_exposed` | `BOOLEAN NULL` |
| `money_unit` | `CHAR(1) NULL` |
| `av_code` | `VARCHAR(10) NULL` |
| `modified_at` | `DATETIME(3) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (item_code, sub_code)`

### `custom_item_master`

| カラム | 最終定義 |
|---|---|
| `custom_id` | `INT UNSIGNED NOT NULL` |
| `kind` | `TINYINT UNSIGNED NOT NULL DEFAULT 10` |
| `item_name` | `VARCHAR(80) NOT NULL DEFAULT ''` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (custom_id)`

### `custom_item_set`

| カラム | 最終定義 |
|---|---|
| `set_id` | `INT UNSIGNED NOT NULL` |
| `custom_id` | `INT UNSIGNED NOT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (set_id, custom_id)`

### `custom_shop_master`

| カラム | 最終定義 |
|---|---|
| `shop_no` | `INT UNSIGNED NOT NULL` |
| `custom_id` | `INT UNSIGNED NOT NULL` |
| `shop_name` | `VARCHAR(80) NOT NULL DEFAULT ''` |
| `description` | `TEXT NULL` |
| `hc_price` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `game_money` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `av_code` | `VARCHAR(30) NULL` |
| `sale_start_at` | `DATETIME(3) NULL` |
| `sale_end_at` | `DATETIME(3) NULL` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (shop_no)`

### `cup_master`

| カラム | 最終定義 |
|---|---|
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `cup_name` | `VARCHAR(40) NOT NULL DEFAULT ''` |
| `short_cup_name` | `VARCHAR(12) NOT NULL DEFAULT ''` |
| `rule_id` | `SMALLINT UNSIGNED NOT NULL DEFAULT 1` |
| `condition_match_count` | `SMALLINT NOT NULL DEFAULT -1` |
| `condition_regular` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `start_at` | `DATETIME(3) NOT NULL` |
| `end_at` | `DATETIME(3) NOT NULL` |
| `nickname_start_at` | `DATETIME(3) NOT NULL` |
| `nickname_end_at` | `DATETIME(3) NOT NULL` |
| `prize` | `VARCHAR(1000) NOT NULL DEFAULT ''` |
| `detail` | `VARCHAR(1000) NOT NULL DEFAULT ''` |
| `status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_id)`

### `cup_channel`

| カラム | 最終定義 |
|---|---|
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `channel_id` | `VARCHAR(30) NOT NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_id, channel_id)`

### `tournament_plan`

| カラム | 最終定義 |
|---|---|
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `seq` | `INT UNSIGNED NOT NULL DEFAULT 1` |
| `cup_name` | `VARCHAR(40) NOT NULL DEFAULT ''` |
| `is_final` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `start_at` | `DATETIME(3) NOT NULL` |
| `end_at` | `DATETIME(3) NOT NULL` |
| `min_level` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `max_level` | `TINYINT UNSIGNED NOT NULL DEFAULT 10` |
| `unit_money` | `INT NOT NULL DEFAULT 0` |
| `max_match_count` | `SMALLINT NOT NULL DEFAULT -1` |
| `min_match_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `prize` | `VARCHAR(1000) NOT NULL DEFAULT ''` |
| `detail` | `VARCHAR(1000) NOT NULL DEFAULT ''` |
| `status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `admin_comment` | `VARCHAR(255) NOT NULL DEFAULT ''` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `rule_id` | `SMALLINT UNSIGNED NOT NULL DEFAULT 1` |
| `notice_url` | `VARCHAR(255) NOT NULL DEFAULT ''` |
| `banner_url` | `VARCHAR(255) NOT NULL DEFAULT ''` |
| `billing_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_id, seq)`
- `INDEX idx_tournament_plan_status (status, start_at)`

### `tournament_limit`

| カラム | 最終定義 |
|---|---|
| `limit_no` | `TINYINT UNSIGNED NOT NULL` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `limit_start_at` | `DATETIME(3) NOT NULL` |
| `limit_end_at` | `DATETIME(3) NOT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (limit_no)`

### `grade_rank_schedule`

| カラム | 最終定義 |
|---|---|
| `rank_date` | `DATE NOT NULL` |
| `batch_flag` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (rank_date)`

### `player_daily_mission`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `mission_id` | `TINYINT UNSIGNED NOT NULL` |
| `progress_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `mission_state` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, mission_id)`
- `CONSTRAINT chk_player_daily_mission_state CHECK (mission_state IN (0, 1, 2))`

### `player_weekly_reward`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `reward_week` | `DATE NOT NULL` |
| `reward_id` | `TINYINT UNSIGNED NOT NULL` |
| `receive_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, reward_week, reward_id)`
- `CONSTRAINT chk_player_weekly_reward_status CHECK (receive_status IN (0, 1, 2))`

### `player_title`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `title_id` | `VARCHAR(10) NOT NULL` |
| `valid_flag` | `CHAR(1) NULL` |
| `acquired_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, title_id)`

### `player_function_item`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `item_code` | `VARCHAR(10) NOT NULL` |
| `quantity` | `INT UNSIGNED NOT NULL DEFAULT 1` |
| `bought_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `expires_at` | `DATETIME(3) NULL` |
| `is_equipped` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, item_code)`

### `player_custom_item`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `custom_id` | `INT UNSIGNED NOT NULL` |
| `quantity` | `SMALLINT UNSIGNED NOT NULL DEFAULT 1` |
| `equip_slot` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `acquired_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, custom_id)`

### `player_present`

| カラム | 最終定義 |
|---|---|
| `present_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `receive_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `present_amount` | `BIGINT NOT NULL DEFAULT 0` |
| `present_type` | `TINYINT UNSIGNED NOT NULL` |
| `present_kind` | `TINYINT UNSIGNED NOT NULL` |
| `present_info` | `VARCHAR(200) NULL` |
| `present_ref_id` | `VARCHAR(20) NULL` |
| `expires_at` | `DATETIME(3) NULL` |
| `sent_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `received_at` | `DATETIME(3) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (present_id)`
- `INDEX idx_player_present_member_status (member_no, receive_status)`
- `CONSTRAINT chk_player_present_status CHECK (receive_status IN (0, 1, 2))`

### `player_grade_rank`

| カラム | 最終定義 |
|---|---|
| `rank_date` | `DATE NOT NULL` |
| `rank_kind` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `rating` | `INT NOT NULL DEFAULT 1500` |
| `grade_level` | `INT NOT NULL DEFAULT 0` |
| `last_played_at` | `DATETIME(3) NULL` |
| `extra_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `last_extra_at` | `DATETIME(3) NULL` |
| `avatar_id` | `VARCHAR(200) NOT NULL DEFAULT ''` |
| `display_flag` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `rank_position` | `INT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (rank_date, rank_kind, member_no)`
- `INDEX idx_player_grade_rank_date_kind_rating (rank_date, rank_kind, rating DESC)`

### `player_yaku_stats`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `y_haitei` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_houtei` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_rinshan` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_tsumo` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_richi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_ippatsu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_yakuhai` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_pinfu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_tanyao` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_iipeikou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chitoitsu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_ittsuu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_toitoi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_sanshoku_jun` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_sanshoku_seq` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_sanshoku_kou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chankan` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_sanankou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_sankantsu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_shosangen` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_honroutou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chanta` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_junchan` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_ryanpeikou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_honisou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chinisou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_wrichi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_dora` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_daisangen` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_suuankou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_suukantsu` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_shosuushi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chinroutou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_tsuisou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_ryuisou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_churenpaotou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_kokushi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_tenhou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_chihou` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_suuankou2` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_daisuushi` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_kokushi2` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `y_churenpaotou2` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `cont_top_max` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `cont_top_now` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `score_max` | `INT NOT NULL DEFAULT 0` |
| `money_max` | `BIGINT NOT NULL DEFAULT 0` |
| `hora_dora_max` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `cont_last_max` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `cont_last_now` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `score_min` | `INT NOT NULL DEFAULT 0` |
| `money_min` | `BIGINT NOT NULL DEFAULT 0` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`

### `cup_player_rating`

| カラム | 最終定義 |
|---|---|
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `cup_point` | `INT NOT NULL DEFAULT 0` |
| `match_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `joined_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `last_played_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_id, member_no)`
- `INDEX idx_cup_player_rating_cup_point (cup_id, cup_point DESC)`

### `tournament_player_rating`

| カラム | 最終定義 |
|---|---|
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `seq` | `INT UNSIGNED NOT NULL DEFAULT 1` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `total_point` | `BIGINT NOT NULL DEFAULT 0` |
| `match_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `point_slot_1` | `BIGINT NULL` |
| `point_slot_2` | `BIGINT NULL` |
| `point_slot_3` | `BIGINT NULL` |
| `point_slot_4` | `BIGINT NULL` |
| `point_slot_5` | `BIGINT NULL` |
| `point_slot_6` | `BIGINT NULL` |
| `point_slot_7` | `BIGINT NULL` |
| `bought_at` | `DATETIME(3) NULL` |
| `joined_at` | `DATETIME(3) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_id, seq, member_no)`
- `INDEX idx_tournament_player_rating_cupseq_point (cup_id, seq, total_point DESC)`

### `tournament_session`

| カラム | 最終定義 |
|---|---|
| `session_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `join_start_at` | `DATETIME(3) NOT NULL` |
| `match_start_at` | `DATETIME(3) NOT NULL` |
| `play_start_at` | `DATETIME(3) NOT NULL` |
| `play_end_at` | `DATETIME(3) NOT NULL` |
| `view_end_at` | `DATETIME(3) NOT NULL` |
| `next_start_at` | `DATETIME(3) NOT NULL` |
| `next_cut_at` | `DATETIME(3) NOT NULL` |
| `play_schedule` | `VARCHAR(200) NOT NULL` |
| `play_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `play_phase` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `player_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `max_player_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `max_room_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `session_name` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `room_option` | `VARCHAR(20) NOT NULL DEFAULT ''` |
| `private_info` | `VARCHAR(20) NULL` |
| `max_viewer_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `play_count` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `play_time` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `play_mode` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `join_money` | `BIGINT NOT NULL DEFAULT 0` |
| `prize_money_1` | `BIGINT NOT NULL DEFAULT 0` |
| `prize_money_2` | `BIGINT NOT NULL DEFAULT 0` |
| `prize_money_3` | `BIGINT NOT NULL DEFAULT 0` |
| `prize_money_4` | `BIGINT NOT NULL DEFAULT 0` |
| `plan_member_no` | `BIGINT UNSIGNED NULL` |
| `result_member_no_1` | `BIGINT UNSIGNED NULL` |
| `result_member_no_2` | `BIGINT UNSIGNED NULL` |
| `result_member_no_3` | `BIGINT UNSIGNED NULL` |
| `result_member_no_4` | `BIGINT UNSIGNED NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (session_id)`
- `INDEX idx_tournament_session_status (play_status, player_count, max_player_count)`

### `tournament_participant`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `session_id` | `BIGINT UNSIGNED NOT NULL` |
| `join_seq_no` | `BIGINT UNSIGNED NOT NULL` |
| `join_member_no` | `CHAR(3) NOT NULL` |
| `join_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `total_manage_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `manage_count` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `last_manage_at` | `DATETIME(3) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`
- `INDEX idx_tournament_participant_member_status (member_no, join_status)`

### `tournament_room`

| カラム | 最終定義 |
|---|---|
| `session_id` | `BIGINT UNSIGNED NOT NULL` |
| `sub_id` | `SMALLINT UNSIGNED NOT NULL` |
| `room_id` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `plan_start_at` | `DATETIME(3) NOT NULL` |
| `plan_end_at` | `DATETIME(3) NOT NULL` |
| `started_at` | `DATETIME(3) NULL` |
| `ended_at` | `DATETIME(3) NULL` |
| `member_no_1` | `BIGINT UNSIGNED NULL` |
| `member_no_2` | `BIGINT UNSIGNED NULL` |
| `member_no_3` | `BIGINT UNSIGNED NULL` |
| `member_no_4` | `BIGINT UNSIGNED NULL` |
| `join_member_no_1` | `CHAR(3) NULL` |
| `join_member_no_2` | `CHAR(3) NULL` |
| `join_member_no_3` | `CHAR(3) NULL` |
| `join_member_no_4` | `CHAR(3) NULL` |
| `score_tmp_1` | `INT NOT NULL DEFAULT 0` |
| `score_tmp_2` | `INT NOT NULL DEFAULT 0` |
| `score_tmp_3` | `INT NOT NULL DEFAULT 0` |
| `score_tmp_4` | `INT NOT NULL DEFAULT 0` |
| `score_1` | `INT NOT NULL DEFAULT 0` |
| `score_2` | `INT NOT NULL DEFAULT 0` |
| `score_3` | `INT NOT NULL DEFAULT 0` |
| `score_4` | `INT NOT NULL DEFAULT 0` |
| `rank1_member_no` | `BIGINT UNSIGNED NULL` |
| `rank2_member_no` | `BIGINT UNSIGNED NULL` |
| `rank3_member_no` | `BIGINT UNSIGNED NULL` |
| `rank4_member_no` | `BIGINT UNSIGNED NULL` |
| `grade1_member_no` | `CHAR(3) NULL` |
| `grade2_member_no` | `CHAR(3) NULL` |
| `grade3_member_no` | `CHAR(3) NULL` |
| `grade4_member_no` | `CHAR(3) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (session_id, sub_id)`

### `channel_runtime`

| カラム | 最終定義 |
|---|---|
| `channel_id` | `VARCHAR(30) NOT NULL` |
| `game_id` | `VARCHAR(10) NOT NULL DEFAULT 'MAJAK4'` |
| `sub_id` | `VARCHAR(5) NOT NULL DEFAULT ''` |
| `go_service` | `VARCHAR(30) NOT NULL DEFAULT ''` |
| `server_ip` | `VARCHAR(50) NOT NULL DEFAULT ''` |
| `server_port` | `MEDIUMINT UNSIGNED NOT NULL DEFAULT 0` |
| `game_port` | `MEDIUMINT UNSIGNED NOT NULL DEFAULT 0` |
| `query_port` | `MEDIUMINT UNSIGNED NOT NULL DEFAULT 0` |
| `channel_name` | `VARCHAR(50) NOT NULL DEFAULT ''` |
| `max_member` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `max_room` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `unit_money` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `member_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `used_room` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `item_yes_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `item_no_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `member_male` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `member_female` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `machine_name` | `VARCHAR(20) NOT NULL DEFAULT ''` |
| `channel_server_version` | `DATETIME(3) NULL` |
| `room_server_version` | `DATETIME(3) NULL` |
| `last_seen_at` | `DATETIME(3) NULL` |
| `zone_id` | `VARCHAR(3) NOT NULL DEFAULT 'JPN'` |
| `scope` | `CHAR(1) NOT NULL DEFAULT 'Z'` |
| `service_mask` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `is_locked` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `description` | `VARCHAR(128) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (channel_id)`

### `event_master`

| カラム | 最終定義 |
|---|---|
| `event_code` | `VARCHAR(10) NOT NULL` |
| `event_no` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `event_name` | `VARCHAR(120) NOT NULL DEFAULT ''` |
| `description` | `VARCHAR(1000) NOT NULL DEFAULT ''` |
| `service_id` | `VARCHAR(20) NOT NULL DEFAULT 'MAJAK4'` |
| `table_info` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `starts_at` | `DATETIME(3) NULL` |
| `ends_at` | `DATETIME(3) NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (event_code, event_no)`
- `INDEX idx_event_master_active (service_id, starts_at, ends_at)`

### `event_user`

| カラム | 最終定義 |
|---|---|
| `event_code` | `VARCHAR(10) NOT NULL` |
| `event_no` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `total_earned_point` | `BIGINT NOT NULL DEFAULT 0` |
| `daily_earned_point` | `BIGINT NOT NULL DEFAULT 0` |
| `total_used_point` | `BIGINT NOT NULL DEFAULT 0` |
| `last_activity_at` | `DATETIME(3) NULL` |
| `registered_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `extra_value1` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value2` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value3` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value4` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value5` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value6` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_value7` | `BIGINT NOT NULL DEFAULT 0` |
| `extra_info1` | `VARCHAR(150) NOT NULL DEFAULT ''` |
| `extra_info2` | `VARCHAR(150) NOT NULL DEFAULT ''` |
| `extra_info3` | `VARCHAR(500) NOT NULL DEFAULT ''` |
| `extra_info4` | `VARCHAR(500) NOT NULL DEFAULT ''` |

キー・インデックス・制約:

- `PRIMARY KEY (event_code, event_no, member_no)`
- `INDEX idx_event_user_member (member_no)`

### `game_admin_member`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `admin_status` | `INT UNSIGNED NOT NULL DEFAULT 0` |
| `is_active` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no)`

### `player_avatar_inventory`

| カラム | 最終定義 |
|---|---|
| `inventory_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `avatar_code` | `VARCHAR(32) NOT NULL` |
| `cost_money` | `BIGINT NOT NULL DEFAULT 0` |
| `cost_gem` | `INT NOT NULL DEFAULT 0` |
| `acquired_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (inventory_id)`
- `INDEX idx_player_avatar_inventory_member (member_no, acquired_at)`
- `INDEX idx_player_avatar_inventory_code (member_no, avatar_code)`
- `CONSTRAINT chk_player_avatar_inventory_cost CHECK (cost_money >= 0 AND cost_gem >= 0)`

### `player_daily_mission_history`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `target_date` | `DATE NOT NULL` |
| `mission_id` | `TINYINT UNSIGNED NOT NULL` |
| `progress_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `mission_state` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, target_date, mission_id)`

### `player_skin`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `skin_no` | `SMALLINT UNSIGNED NOT NULL` |
| `is_attached` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `expires_at` | `DATETIME(3) NOT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, skin_no)`
- `INDEX idx_player_skin_expiry (member_no, expires_at)`

### `player_shop`

| カラム | 最終定義 |
|---|---|
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `shop_id` | `SMALLINT UNSIGNED NOT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `opened_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (member_no, shop_id)`

### `memorial_shop_master`

| カラム | 最終定義 |
|---|---|
| `shop_id` | `SMALLINT UNSIGNED NOT NULL` |
| `shop_name` | `VARCHAR(20) NOT NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (shop_id)`

### `event_gift_master`

| カラム | 最終定義 |
|---|---|
| `event_code` | `VARCHAR(20) NOT NULL` |
| `event_no` | `INT UNSIGNED NOT NULL` |
| `gift_code` | `VARCHAR(20) NOT NULL` |
| `gift_name` | `VARCHAR(100) NULL` |
| `gift_value` | `BIGINT NULL` |
| `gift_type` | `CHAR(1) NULL` |
| `total_limit_count` | `INT UNSIGNED NULL` |
| `daily_limit_count` | `INT UNSIGNED NULL` |
| `mission_no` | `INT NOT NULL DEFAULT 0` |
| `gift_message` | `VARCHAR(500) NULL` |
| `gift_avatar_id` | `VARCHAR(300) NULL` |
| `gift_group` | `VARCHAR(10) NULL` |
| `gift_sender_id` | `VARCHAR(20) NULL` |
| `created_at` | `DATETIME(3) NULL` |
| `updated_at` | `DATETIME(3) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (event_code, event_no, gift_code)`

### `serial_exchange_item`

| カラム | 最終定義 |
|---|---|
| `event_code` | `VARCHAR(20) NOT NULL` |
| `event_no` | `INT UNSIGNED NOT NULL` |
| `service_id` | `VARCHAR(20) NOT NULL` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `gift_code` | `VARCHAR(20) NOT NULL` |
| `gift_value` | `BIGINT NOT NULL DEFAULT 0` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (event_code, event_no, service_id, member_no, gift_code)`

### `serial_coupon`

| カラム | 最終定義 |
|---|---|
| `event_code` | `VARCHAR(20) NOT NULL` |
| `event_no` | `INT UNSIGNED NOT NULL` |
| `mission_no` | `INT NOT NULL` |
| `coupon_no` | `VARCHAR(100) NOT NULL` |
| `inquiry_check_no` | `VARCHAR(30) NULL` |
| `gift_code` | `VARCHAR(20) NULL` |
| `inquiry_comment` | `VARCHAR(400) NULL` |
| `valid_check` | `CHAR(1) NULL` |
| `member_no` | `BIGINT UNSIGNED NULL` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (event_code, event_no, mission_no, coupon_no)`
- `INDEX idx_serial_coupon_member (member_no)`

### `game_clear_count`

| カラム | 最終定義 |
|---|---|
| `game_id` | `VARCHAR(20) NOT NULL` |
| `game_description` | `VARCHAR(256) NULL` |
| `count_description` | `VARCHAR(256) NULL` |
| `count_image_url` | `VARCHAR(256) NULL` |
| `clear_count` | `BIGINT NOT NULL DEFAULT 0` |
| `admin_no` | `BIGINT UNSIGNED NULL` |
| `count_status` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `created_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `updated_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (game_id)`

## 7. ログDBカラム定義

### `game_session_log`

| カラム | 最終定義 |
|---|---|
| `game_session_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `played_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `channel_id` | `VARCHAR(30) NOT NULL` |
| `room_id` | `INT UNSIGNED NOT NULL` |
| `is_private` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `room_option` | `VARCHAR(200) NOT NULL DEFAULT ''` |
| `money_rate` | `BIGINT NOT NULL DEFAULT 0` |
| `minimum_money` | `BIGINT NOT NULL DEFAULT 0` |
| `maximum_money` | `BIGINT NOT NULL DEFAULT 0` |
| `minimum_class` | `TINYINT UNSIGNED NULL` |
| `maximum_class` | `TINYINT UNSIGNED NULL` |
| `cup_id` | `BIGINT UNSIGNED NULL` |
| `rule_id` | `SMALLINT UNSIGNED NULL` |
| `cup_sequence` | `BIGINT UNSIGNED NULL` |
| `used_ticket` | `SMALLINT UNSIGNED NULL` |
| `cup_rule` | `TINYINT UNSIGNED NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (game_session_id)`
- `INDEX idx_game_session_log_played_at (played_at)`
- `INDEX idx_game_session_log_channel_played (channel_id, played_at)`

### `game_player_result_log`

| カラム | 最終定義 |
|---|---|
| `game_player_result_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `game_session_id` | `BIGINT UNSIGNED NOT NULL` |
| `played_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `was_connected` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `ranking` | `TINYINT UNSIGNED NOT NULL` |
| `score` | `INT NOT NULL DEFAULT 0` |
| `point` | `INT NOT NULL DEFAULT 0` |
| `had_yakitori` | `BOOLEAN NOT NULL DEFAULT FALSE` |
| `chip` | `INT NOT NULL DEFAULT 0` |
| `money_before` | `BIGINT NOT NULL DEFAULT 0` |
| `lent_money_before` | `BIGINT NOT NULL DEFAULT 0` |
| `dealer_fee` | `BIGINT NOT NULL DEFAULT 0` |
| `money_change` | `BIGINT NOT NULL DEFAULT 0` |
| `money_after` | `BIGINT NOT NULL DEFAULT 0` |
| `lent_money_after` | `BIGINT NOT NULL DEFAULT 0` |
| `ip_address` | `VARCHAR(45) NOT NULL DEFAULT ''` |
| `gateway` | `VARCHAR(45) NOT NULL DEFAULT ''` |
| `mac_address` | `VARCHAR(17) NOT NULL DEFAULT ''` |
| `previous_ticket` | `BIGINT NULL` |
| `returned_ticket` | `BIGINT NULL` |
| `previous_class` | `TINYINT UNSIGNED NULL` |
| `current_class` | `TINYINT UNSIGNED NULL` |
| `current_ticket` | `BIGINT NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (game_player_result_id)`
- `UNIQUE KEY uq_game_player_result_session_member (game_session_id, member_no)`
- `INDEX idx_game_player_result_member_played (member_no, played_at)`
- `CONSTRAINT chk_game_player_result_ranking CHECK (ranking BETWEEN 1 AND 4)`

### `training_session_log`

| カラム | 最終定義 |
|---|---|
| `training_session_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `played_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `channel_id` | `VARCHAR(30) NOT NULL` |
| `room_id` | `INT UNSIGNED NOT NULL` |
| `room_option` | `VARCHAR(200) NOT NULL DEFAULT ''` |
| `player_count` | `TINYINT UNSIGNED NOT NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (training_session_id)`
- `INDEX idx_training_session_log_played_at (played_at)`
- `CONSTRAINT chk_training_session_player_count CHECK (player_count BETWEEN 1 AND 4)`

### `training_player_result_log`

| カラム | 最終定義 |
|---|---|
| `training_player_result_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `training_session_id` | `BIGINT UNSIGNED NOT NULL` |
| `seat_order` | `TINYINT UNSIGNED NOT NULL` |
| `member_no` | `BIGINT UNSIGNED NULL` |
| `point` | `INT NOT NULL DEFAULT 0` |

キー・インデックス・制約:

- `PRIMARY KEY (training_player_result_id)`
- `UNIQUE KEY uq_training_player_result_seat (training_session_id, seat_order)`
- `INDEX idx_training_player_result_member (member_no)`
- `CONSTRAINT chk_training_player_result_seat CHECK (seat_order BETWEEN 0 AND 3)`

### `weekly_reward_claim_log`

| カラム | 最終定義 |
|---|---|
| `weekly_reward_claim_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `reward_week` | `DATE NOT NULL` |
| `reward_id` | `INT UNSIGNED NOT NULL` |
| `receive_status` | `TINYINT UNSIGNED NOT NULL` |
| `claimed_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (weekly_reward_claim_id)`
- `UNIQUE KEY uq_weekly_reward_claim (member_no, reward_week, reward_id)`
- `INDEX idx_weekly_reward_claim_week (reward_week, claimed_at)`

### `money_transaction_log`

| カラム | 最終定義 |
|---|---|
| `money_transaction_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `event_code` | `VARCHAR(32) NOT NULL` |
| `event_title` | `VARCHAR(100) NOT NULL DEFAULT ''` |
| `game_id` | `VARCHAR(20) NOT NULL DEFAULT 'MAJAK4'` |
| `amount` | `BIGINT NOT NULL` |
| `balance_before` | `BIGINT NOT NULL` |
| `balance_after` | `BIGINT NOT NULL` |
| `is_valid` | `BOOLEAN NOT NULL DEFAULT TRUE` |
| `order_number` | `VARCHAR(64) NULL` |
| `additional_info` | `VARCHAR(100) NULL` |
| `billing_order_number` | `VARCHAR(20) NULL` |
| `unit_count` | `INT UNSIGNED NOT NULL DEFAULT 1` |
| `remote_address` | `VARCHAR(45) NOT NULL DEFAULT ''` |

キー・インデックス・制約:

- `PRIMARY KEY (money_transaction_id)`
- `INDEX idx_money_transaction_member_occurred (member_no, occurred_at)`
- `INDEX idx_money_transaction_event_occurred (event_code, occurred_at)`

### `winning_yaku_log`

| カラム | 最終定義 |
|---|---|
| `winning_yaku_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `game_id` | `VARCHAR(20) NOT NULL DEFAULT 'MAJAK4'` |
| `yaku_code` | `INT NOT NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (winning_yaku_log_id)`
- `INDEX idx_winning_yaku_member_occurred (member_no, occurred_at)`
- `INDEX idx_winning_yaku_code_occurred (yaku_code, occurred_at)`

### `item_purchase_log`

| カラム | 最終定義 |
|---|---|
| `item_purchase_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `purchased_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `item_code` | `VARCHAR(64) NOT NULL` |
| `quantity` | `INT UNSIGNED NOT NULL DEFAULT 1` |
| `unit_price` | `BIGINT NOT NULL DEFAULT 0` |
| `external_user_no` | `VARCHAR(64) NULL` |
| `purchase_channel` | `INT UNSIGNED NOT NULL DEFAULT 2` |
| `order_number` | `VARCHAR(64) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (item_purchase_id)`
- `INDEX idx_item_purchase_member_purchased (member_no, purchased_at)`
- `INDEX idx_item_purchase_item_purchased (item_code, purchased_at)`

### `gem_transaction_log`

| カラム | 最終定義 |
|---|---|
| `id` | `BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `event_type` | `VARCHAR(30) NOT NULL` |
| `amount` | `INT NOT NULL` |
| `balance_before` | `INT UNSIGNED NOT NULL` |
| `balance_after` | `INT UNSIGNED NOT NULL` |
| `ref_id` | `VARCHAR(64) NULL` |
| `memo` | `VARCHAR(200) NULL` |
| `operator_no` | `BIGINT UNSIGNED NULL` |
| `client_ip` | `VARCHAR(45) NULL` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `INDEX idx_gem_tx_member (member_no, occurred_at)`
- `INDEX idx_gem_tx_type (event_type, occurred_at)`
- `INDEX idx_gem_tx_ref (ref_id)`
- `INDEX idx_gem_tx_date (occurred_at)`

### `admin_operation_log`

| カラム | 最終定義 |
|---|---|
| `id` | `BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY` |
| `operator_no` | `BIGINT UNSIGNED NOT NULL` |
| `operator_role` | `VARCHAR(20) NOT NULL` |
| `action` | `VARCHAR(50) NOT NULL` |
| `target_type` | `VARCHAR(50) NULL` |
| `target_id` | `VARCHAR(100) NULL` |
| `payload_before` | `JSON NULL` |
| `payload_after` | `JSON NULL` |
| `memo` | `VARCHAR(500) NULL` |
| `client_ip` | `VARCHAR(45) NULL` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `INDEX idx_admin_op_operator (operator_no, occurred_at)`
- `INDEX idx_admin_op_target (target_type, target_id)`
- `INDEX idx_admin_op_date (occurred_at)`

### `player_login_log`

| カラム | 最終定義 |
|---|---|
| `login_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `event_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `ip_address` | `VARCHAR(45) NOT NULL DEFAULT ''` |
| `user_agent` | `VARCHAR(200) NOT NULL DEFAULT ''` |

キー・インデックス・制約:

- `PRIMARY KEY (login_log_id, occurred_at)`
- `INDEX idx_player_login_member_occurred (member_no, occurred_at)`
- `CONSTRAINT chk_player_login_event_type CHECK (event_type IN (0, 1, 2))`

### `daily_mission_completion_log`

| カラム | 最終定義 |
|---|---|
| `completion_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `target_date` | `DATE NOT NULL` |
| `mission_id` | `TINYINT UNSIGNED NOT NULL` |
| `progress_count` | `SMALLINT UNSIGNED NOT NULL DEFAULT 0` |
| `mission_state` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `completed_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (completion_log_id)`
- `UNIQUE KEY uq_daily_mission_completion (member_no, target_date, mission_id)`
- `INDEX idx_daily_mission_completion_date (target_date)`

### `custom_item_purchase_log`

| カラム | 最終定義 |
|---|---|
| `purchase_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `shop_no` | `INT UNSIGNED NULL` |
| `custom_id` | `INT UNSIGNED NOT NULL` |
| `source_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 1` |
| `gem_price` | `INT NOT NULL DEFAULT 0` |
| `hc_price` | `INT NOT NULL DEFAULT 0` |
| `game_money` | `INT NOT NULL DEFAULT 0` |
| `order_id` | `VARCHAR(64) NULL` |

キー・インデックス・制約:

- `PRIMARY KEY (purchase_log_id)`
- `INDEX idx_custom_item_purchase_member (member_no, occurred_at)`
- `INDEX idx_custom_item_purchase_item (custom_id, occurred_at)`

### `present_delivery_log`

| カラム | 最終定義 |
|---|---|
| `delivery_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `occurred_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `present_id` | `BIGINT UNSIGNED NOT NULL` |
| `event_type` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `present_type` | `TINYINT UNSIGNED NOT NULL` |
| `present_amount` | `BIGINT NOT NULL DEFAULT 0` |
| `admin_email` | `VARCHAR(254) NULL` |
| `reason` | `VARCHAR(200) NOT NULL DEFAULT ''` |

キー・インデックス・制約:

- `PRIMARY KEY (delivery_log_id)`
- `INDEX idx_present_delivery_member (member_no, occurred_at)`

### `grade_rank_snapshot_log`

| カラム | 最終定義 |
|---|---|
| `snapshot_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `snapshot_date` | `DATE NOT NULL` |
| `rank_kind` | `TINYINT UNSIGNED NOT NULL DEFAULT 0` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `rating` | `INT NOT NULL DEFAULT 1500` |
| `grade_level` | `INT NOT NULL DEFAULT 0` |
| `rank_position` | `INT NULL` |
| `snapshotted_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |

キー・インデックス・制約:

- `PRIMARY KEY (snapshot_log_id)`
- `INDEX idx_grade_rank_snapshot_date (snapshot_date, rank_kind, rank_position)`

### `cup_match_log`

| カラム | 最終定義 |
|---|---|
| `cup_match_log_id` | `BIGINT UNSIGNED NOT NULL AUTO_INCREMENT` |
| `played_at` | `DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)` |
| `cup_id` | `INT UNSIGNED NOT NULL` |
| `game_session_id` | `BIGINT UNSIGNED NULL` |
| `member_no` | `BIGINT UNSIGNED NOT NULL` |
| `ranking` | `TINYINT UNSIGNED NOT NULL DEFAULT 1` |
| `point_change` | `INT NOT NULL DEFAULT 0` |
| `point_after` | `INT NOT NULL DEFAULT 0` |

キー・インデックス・制約:

- `PRIMARY KEY (cup_match_log_id)`
- `INDEX idx_cup_match_log_cup_member (cup_id, member_no, played_at)`
- `CONSTRAINT chk_cup_match_log_ranking CHECK (ranking BETWEEN 1 AND 4)`

## 8. レガシーDDL対応表

対象は通常拡張子51件と誤記拡張子2件を合わせた全53件である。

| レガシーDDL | 正規化先 | 方針 |
|---|---|---|
| `CHANELMAST.ddl.sql` | `channel_master` | 直接移行（DB接続情報・実行パスを除外） |
| `CHANELWT.ddl.sql` | `channel_runtime` | 直接移行（運用スナップショットのため初期投入なし） |
| `EVTCODEMAST.ddl.sql` | `event_master` | 直接移行 |
| `EVTCOUPONLIST.ddl.sql` | `serial_coupon` | 直接移行 |
| `EVTEXCHGITEM.ddl.sql` | `serial_exchange_item` | 直接移行 |
| `EVTGIFTMAST.ddl.sql` | `event_gift_master` | 直接移行 |
| `EVTUSERMAST.ddl.sql` | `event_user` | 直接移行 |
| `GAMEMONEYHIST.ddl.sql` | `money_transaction_log` | 正規化移行 |
| `ITEMBUYHIST.ddl.sql` | `item_purchase_log` | 正規化移行 |
| `ITEMMAST.ddl.sql` | `billing_item_master` | 直接移行 |
| `KT_GAMECNTMAST.ddl.sql` | `game_clear_count` | 直接移行 |
| `MAJAK2TRAININGHIST.ddl.sql` | `training_session_log / training_player_result_log` | 1対多へ正規化 |
| `MAJAK3YAKUHIST.ddl.sql` | `winning_yaku_log` | 直接移行 |
| `MAJAKCUPCHANELMT.ddl.sql` | `cup_channel` | 直接移行 |
| `MAJAKCUPMAST.ddl.sql` | `cup_master` | 直接移行 |
| `MAJAKCUPRAT.ddl.sql` | `cup_player_rating` | 直接移行 |
| `MAJAKRULEMAST.ddl.sql` | `rule_master` | 直接移行 |
| `MJK_CUSTOMITEMMAST.ddl.sql` | `custom_item_master` | 直接移行 |
| `MJK_CUSTOMSETMAST.ddl.sql` | `custom_item_set` | 直接移行 |
| `MJK_CUSTOMSHOPMAST.ddl.sql` | `custom_shop_master` | 直接移行 |
| `MJK_DAILYMISSIONHIST.ddl.sql` | `player_daily_mission_history / daily_mission_completion_log` | 状態とログへ分離 |
| `MJK_DAILYMISSIONLIST.ddl.sql` | `player_daily_mission` | 直接移行 |
| `MJK_DAILYMISSIONMAST.ddl.sql` | `daily_mission_master` | 直接移行 |
| `MJK_EVTMAST.ddl.sql` | `tournament_plan` | 直接移行 |
| `MJK_EVTRAT.ddl.sql` | `tournament_player_rating` | 直接移行 |
| `MJK_GRADEMANAGE.ddl.sql` | `grade_rank_schedule` | 直接移行 |
| `MJK_GRADERANK.ddl.sql` | `player_grade_rank / grade_rank_snapshot_log` | 状態とログへ分離 |
| `MJK_GRADERAT.ddl.sql` | `player_mode_stats (grade)` | モード統合 |
| `MJK_HICLASSRAT.ddl.sql` | `player_mode_stats (high_class) / player_high_class_summary / player_yaku_stats` | 統計を正規化 |
| `MJK_ITEMLIST.ddl.sql` | `player_function_item` | 直接移行 |
| `MJK_ITEMMAST.ddl.sql` | `function_item_master` | 直接移行 |
| `MJK_TITLELIST.ddl.sql` | `player_title` | 直接移行 |
| `MJK_TITLEMAST.ddl.sql` | `title_master` | 直接移行 |
| `MJK_TOURNAMENTDETAIL.ddl.sql` | `tournament_room` | 直接移行 |
| `MJK_TOURNAMENTJOIN.ddl.sql` | `tournament_participant` | 直接移行 |
| `MJK_TOURNAMENTLIMIT.ddl.sql` | `tournament_limit` | 直接移行 |
| `MJK_TOURNAMENTPLAN.ddl.sql` | `tournament_session` | 直接移行 |
| `MJK_USERCUSTOMITEM.ddl.ql.sql` | `player_custom_item` | 直接移行（旧ファイル名の誤記を含む） |
| `MJK_USERHIST.ddl.sql` | `game_player_result_log` | 直接移行 |
| `MJK_USERPRESENT.ddl.sql` | `player_present / present_delivery_log` | 状態とログへ分離 |
| `MJK_WEEKLYREWARDLIST.ddl.sql` | `player_weekly_reward / weekly_reward_claim_log` | 対象週を追加して履歴分離 |
| `MJK_WEEKLYREWARDMAST.ddl.sql` | `weekly_reward_master` | 直接移行 |
| `MJKAGARIRAT.ddl.sql` | `player_mode_stats (agari)` | モード統合 |
| `MJKCOMMONRAT.ddl.sql` | `player_wallet / player_profile / player_mode_stats` | プレイヤー状態を分離 |
| `MJKCOMPETERAT.ddl.sql` | `player_mode_stats (compete)` | モード統合 |
| `MJKHANGERAT.ddl.sql` | `player_mode_stats (regular) / player_profile` | 通常戦績と連勝敗を分離 |
| `MJKHGDPRAT.ddl.sql` | `player_mode_stats (hgdp)` | モード統合 |
| `MJKSHOPMAST.ddl.sql` | `memorial_shop_master` | 直接移行 |
| `MJKUSERSHOPLIST.ddl.sql` | `player_shop` | 直接移行 |
| `MJKUSERSKINLIST.ddl.sql` | `player_skin` | 直接移行 |
| `MJL_GAMEHIST.ddl.sql` | `game_session_log` | 大会ログ項目を含めて移行 |
| `MJL_USERHIST.ddl.ql.sql` | `game_player_result_log` | チケット・クラス項目を含めて移行（旧ファイル名の誤記を含む） |
| `PROCODET.ddl.sql` | `transaction_code_master` | 直接移行 |

## 9. 意図的に保持しない旧項目

- `CHANELMAST` のOracle接続文字列、ユーザー、パスワード、実行ファイルパスは秘密情報・配備情報でありDBへ移行しない。
- レーティング各表の `_U` 列は旧サーバーの一時ロールバック影であり、現行値を正本とする。`MJKCOMMONRAT.GAMMONEY_U` だけは `player_wallet.pending_game_money` として保持する。
- `MJKCOMMONRAT.SLEVEL` はコイン量から算出する表示名であり、`best_money_level` とサーバーのレベル算出処理を正本とする。
- `MJK_WEEKLYREWARDLIST` の対象週は旧表の登録日時から週初日を導出し、`reward_week` として保持する。
