-- MySQL 8.0 以上
-- 対象データベース: majak_game
-- ゲームデータベース の新規構築用基準スキーマ
-- 外部キー制約は使用しない

-- テーブル: player_account
CREATE TABLE player_account (
    member_no          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    display_name       VARCHAR(100) NOT NULL DEFAULT '',
    email              VARCHAR(254) NULL,
    google_sub         VARCHAR(64)  NULL,
    sex_code           CHAR(1)      NOT NULL DEFAULT 'U',
    avatar_id          VARCHAR(255) NOT NULL DEFAULT '',
    terms_agreed_at    DATETIME(3)  NULL,
    approved_at        DATETIME(3)  NULL,
    approved_by        BIGINT UNSIGNED NULL,
    reject_reason      VARCHAR(200) NULL,
    account_status     TINYINT UNSIGNED NOT NULL DEFAULT 0,
    source_environment VARCHAR(16)  NOT NULL DEFAULT 'production',
    first_login_at     DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    last_login_at      DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    created_at         DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at         DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                      ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no),
    UNIQUE KEY idx_player_account_google_sub (google_sub),
    INDEX idx_player_account_status_created (account_status, created_at),
    CONSTRAINT chk_player_account_sex
        CHECK (sex_code IN ('M', 'F', 'U')),
    CONSTRAINT chk_player_account_status
        CHECK (account_status IN (0, 1, 2))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_wallet
CREATE TABLE player_wallet (
    member_no          BIGINT UNSIGNED NOT NULL,
    game_money         BIGINT      NOT NULL DEFAULT 1000,
    pending_game_money BIGINT      NOT NULL DEFAULT 0,
    earned_game_money  BIGINT      NOT NULL DEFAULT 0,
    loaned_game_money  BIGINT      NOT NULL DEFAULT 0,
    gem_count          INT         NOT NULL DEFAULT 0,
    row_version        BIGINT UNSIGNED NOT NULL DEFAULT 0,
    created_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                   ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no),
    CONSTRAINT chk_player_wallet_game_money CHECK (game_money >= 0),
    CONSTRAINT chk_player_wallet_gem_count CHECK (gem_count >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_profile
CREATE TABLE player_profile (
    member_no          BIGINT UNSIGNED NOT NULL,
    common_rating      INT         NOT NULL DEFAULT 1400,
    experience         INT         NOT NULL DEFAULT 0,
    best_money_level   TINYINT UNSIGNED NOT NULL DEFAULT 2,
    consecutive_win_loss INT       NOT NULL DEFAULT 0,
    all_in_count       INT UNSIGNED NOT NULL DEFAULT 0,
    last_all_in_at     DATETIME(3) NULL,
    trick_title_code   VARCHAR(32) NULL,
    majak_title_code   VARCHAR(32) NULL,
    event_open_flag    CHAR(1)     NULL,
    weekly_point       INT         NOT NULL DEFAULT 0,
    weekly_target_date DATE        NOT NULL DEFAULT (CURRENT_DATE),
    joined_at          DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    last_played_at     DATETIME(3) NULL,
    row_version        BIGINT UNSIGNED NOT NULL DEFAULT 0,
    created_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                   ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no),
    CONSTRAINT chk_player_profile_experience CHECK (experience >= 0),
    CONSTRAINT chk_player_profile_all_in_count CHECK (all_in_count >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_mode_stats
CREATE TABLE player_mode_stats (
    member_no          BIGINT UNSIGNED NOT NULL,
    mode_code          VARCHAR(20) NOT NULL,
    rating             INT         NOT NULL DEFAULT 1400,
    match_count        INT UNSIGNED NOT NULL DEFAULT 0,
    win_count          INT UNSIGNED NOT NULL DEFAULT 0,
    defeat_count       INT UNSIGNED NOT NULL DEFAULT 0,
    draw_count         INT UNSIGNED NOT NULL DEFAULT 0,
    first_count        INT UNSIGNED NOT NULL DEFAULT 0,
    second_count       INT UNSIGNED NOT NULL DEFAULT 0,
    third_count        INT UNSIGNED NOT NULL DEFAULT 0,
    fourth_count       INT UNSIGNED NOT NULL DEFAULT 0,
    turn_count         INT UNSIGNED NOT NULL DEFAULT 0,
    dealer_count       INT UNSIGNED NOT NULL DEFAULT 0,
    point_sum          BIGINT       NOT NULL DEFAULT 0,
    round_count        INT UNSIGNED NOT NULL DEFAULT 0,
    win_hand_count     INT UNSIGNED NOT NULL DEFAULT 0,
    win_hand_points    BIGINT       NOT NULL DEFAULT 0,
    deal_in_count      INT UNSIGNED NOT NULL DEFAULT 0,
    deal_in_points     BIGINT       NOT NULL DEFAULT 0,
    riichi_count       INT UNSIGNED NOT NULL DEFAULT 0,
    meld_count         INT UNSIGNED NOT NULL DEFAULT 0,
    tip_point          BIGINT       NOT NULL DEFAULT 0,
    tip_match_count    INT UNSIGNED NOT NULL DEFAULT 0,
    bust_count         INT UNSIGNED NOT NULL DEFAULT 0,
    bust_other_count   INT UNSIGNED NOT NULL DEFAULT 0,
    dora_count         INT UNSIGNED NOT NULL DEFAULT 0,
    ura_dora_count     INT UNSIGNED NOT NULL DEFAULT 0,
    riichi_win_count   INT UNSIGNED NOT NULL DEFAULT 0,
    disconnect_count   INT UNSIGNED NOT NULL DEFAULT 0,
    last_disconnect_at DATETIME(3) NULL,
    last_channel_id    VARCHAR(30) NULL,
    grade_level        INT         NOT NULL DEFAULT 0,
    grade_point        INT         NOT NULL DEFAULT 0,
    extra_count        INT UNSIGNED NOT NULL DEFAULT 0,
    last_extra_at      DATETIME(3) NULL,
    created_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                   ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, mode_code),
    CONSTRAINT chk_player_mode_stats_mode
        CHECK (mode_code IN ('regular', 'compete', 'high_class', 'grade', 'agari', 'hgdp'))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_high_class_summary
CREATE TABLE player_high_class_summary (
    member_no          BIGINT UNSIGNED NOT NULL,
    score_max               INT NULL,
    score_min               INT NULL,
    money_max               BIGINT NULL,
    money_min               BIGINT NULL,
    win_hand_dora_max       INT NOT NULL DEFAULT 0,
    consecutive_top_max     INT UNSIGNED NOT NULL DEFAULT 0,
    consecutive_top_current INT UNSIGNED NOT NULL DEFAULT 0,
    created_at              DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at              DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                       ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_high_class_yaku
CREATE TABLE player_high_class_yaku (
    member_no          BIGINT UNSIGNED NOT NULL,
    yaku_id   SMALLINT UNSIGNED NOT NULL,
    count     INT UNSIGNED NOT NULL DEFAULT 0,
    updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, yaku_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: gem_product_master
CREATE TABLE gem_product_master (
    product_id       VARCHAR(50)    NOT NULL,
    display_name     VARCHAR(100)   NOT NULL,
    gem_amount       INT UNSIGNED   NOT NULL,
    price_jpy        INT UNSIGNED   NOT NULL,
    platform         ENUM('web','ios','android','all')
                                    NOT NULL DEFAULT 'all',
    store_product_id VARCHAR(200)   NULL,
    is_active        BOOLEAN        NOT NULL DEFAULT TRUE,
    sort_order       SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    created_at       DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at       DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (product_id),
    CONSTRAINT chk_gem_product_gem_amount CHECK (gem_amount > 0),
    CONSTRAINT chk_gem_product_price_jpy  CHECK (price_jpy > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: gem_charge_order
CREATE TABLE gem_charge_order (
    order_id         VARCHAR(64)    NOT NULL,
    member_no          BIGINT UNSIGNED NOT NULL,
    product_id       VARCHAR(50)    NOT NULL,
    platform         ENUM('web','ios','android') NOT NULL,
    gem_amount       INT UNSIGNED   NOT NULL,
    price_jpy        INT UNSIGNED   NOT NULL,
    status           ENUM('pending','completed','failed','refunded')
                                    NOT NULL DEFAULT 'pending',
    pg_txn_id        VARCHAR(200)   NULL,
    pg_raw_response  TEXT           NULL,
    client_ip        VARCHAR(45)    NULL,
    created_at       DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    completed_at     DATETIME(3)    NULL,
    PRIMARY KEY (order_id),
    INDEX idx_gem_order_member   (member_no),
    INDEX idx_gem_order_status   (status, created_at),
    INDEX idx_gem_order_pg_txn   (pg_txn_id),
    CONSTRAINT chk_gem_order_gem_amount CHECK (gem_amount > 0),
    CONSTRAINT chk_gem_order_price_jpy  CHECK (price_jpy > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: gem_item_price
CREATE TABLE gem_item_price (
    item_key         VARCHAR(60)    NOT NULL,
    item_type        ENUM('custom_item','function_item') NOT NULL,
    gem_price        INT UNSIGNED   NOT NULL DEFAULT 0,
    game_money_price INT UNSIGNED   NOT NULL DEFAULT 0,
    sale_start_at    DATETIME(3)    NULL,
    sale_end_at      DATETIME(3)    NULL,
    is_active        BOOLEAN        NOT NULL DEFAULT TRUE,
    created_at       DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at       DATETIME(3)    NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (item_key, item_type),
    CONSTRAINT chk_gem_item_price_gem      CHECK (gem_price >= 0),
    CONSTRAINT chk_gem_item_price_money    CHECK (game_money_price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: admin_account
CREATE TABLE admin_account (
    admin_no    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    email        VARCHAR(200)  NOT NULL,
    role         ENUM('super_admin','operator','viewer') NOT NULL DEFAULT 'operator',
    is_active    BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at   DATETIME(3)   NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at   DATETIME(3)   NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                               ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (admin_no),
    UNIQUE KEY uq_admin_account_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: transaction_code_master
CREATE TABLE transaction_code_master (
    transaction_code   VARCHAR(20)  NOT NULL,
    code_title         VARCHAR(30)  NULL,
    is_history_enabled BOOLEAN      NOT NULL DEFAULT FALSE,
    is_cumulative      BOOLEAN      NOT NULL DEFAULT FALSE,
    open_status        CHAR(1)      NULL,
    start_date         DATE         NULL,
    content            VARCHAR(80)  NULL,
    service_code       VARCHAR(10)  NULL,
    service_name       VARCHAR(30)  NULL,
    is_service_enabled BOOLEAN      NOT NULL DEFAULT FALSE,
    game_id            VARCHAR(10)  NULL,
    registrant_name    VARCHAR(20)  NULL,
    planner_name       VARCHAR(20)  NULL,
    developer_name     VARCHAR(20)  NULL,
    direction_code     CHAR(1)      NULL,
    avatar_code        VARCHAR(10)  NULL,
    PRIMARY KEY (transaction_code),
    INDEX idx_transaction_code_master_avatar (avatar_code),
    INDEX idx_transaction_code_master_game (game_id, is_history_enabled)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: channel_master
CREATE TABLE channel_master (
    channel_id      VARCHAR(30)     NOT NULL,
    game_id         VARCHAR(20)     NOT NULL DEFAULT 'MAJAK4',
    sub_id          VARCHAR(10)     NOT NULL,
    channel_name    VARCHAR(100)    NOT NULL DEFAULT '',
    max_member      INT UNSIGNED    NOT NULL DEFAULT 1024,
    max_room        INT UNSIGNED    NOT NULL DEFAULT 256,
    unit_money      INT UNSIGNED    NOT NULL DEFAULT 20,
    channel_type    TINYINT UNSIGNED NOT NULL DEFAULT 0,

    env             VARCHAR(10)     NOT NULL DEFAULT 'prod',
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    server_url      VARCHAR(255)    NOT NULL DEFAULT '',
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (channel_id),
    INDEX idx_channel_master_subid   (sub_id),
    INDEX idx_channel_master_gameid  (game_id, env, is_active)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: rule_master
CREATE TABLE rule_master (
    rule_id                 SMALLINT UNSIGNED   NOT NULL,
    judgement_type          TINYINT UNSIGNED    NOT NULL DEFAULT 0,
    room_option             CHAR(13)            NOT NULL DEFAULT '0000000000000',
    normal_yaku_condition   CHAR(28)            NOT NULL DEFAULT '0000000000000000000000000000',
    yakuman_condition       CHAR(15)            NOT NULL DEFAULT '000000000000000',
    rule_name               VARCHAR(100)        NOT NULL DEFAULT '',
    rule_detail             VARCHAR(2000)       NOT NULL DEFAULT '',
    evt_sum_type            INT UNSIGNED        NULL,
    PRIMARY KEY (rule_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: title_master
CREATE TABLE title_master (
    title_id        VARCHAR(10)     NOT NULL,
    title_name      VARCHAR(30)     NOT NULL DEFAULT '',
    img_filename    VARCHAR(20)     NOT NULL DEFAULT '',
    attribute       CHAR(1)         NOT NULL DEFAULT 'T',
    title_rank      TINYINT UNSIGNED NOT NULL DEFAULT 1,
    description     VARCHAR(200)    NULL,
    PRIMARY KEY (title_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: daily_mission_master
CREATE TABLE daily_mission_master (
    mission_id      TINYINT UNSIGNED NOT NULL,
    condition_type  TINYINT UNSIGNED NOT NULL,

    condition_count SMALLINT UNSIGNED NOT NULL DEFAULT 1,
    point           TINYINT UNSIGNED NOT NULL DEFAULT 5,
    PRIMARY KEY (mission_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: weekly_reward_master
CREATE TABLE weekly_reward_master (
    reward_id       TINYINT UNSIGNED NOT NULL,
    reward_type     TINYINT UNSIGNED NOT NULL DEFAULT 1,

    reward_count    INT UNSIGNED    NOT NULL DEFAULT 0,
    required_point  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    PRIMARY KEY (reward_id),
    CONSTRAINT chk_weekly_reward_type CHECK (reward_type IN (1, 2))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: function_item_master
CREATE TABLE function_item_master (
    item_code       VARCHAR(10)     NOT NULL,
    item_name       VARCHAR(24)     NOT NULL DEFAULT '',
    category_no     SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    sub_no          SMALLINT UNSIGNED NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (item_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: billing_item_master
CREATE TABLE billing_item_master (
    item_code       VARCHAR(5)      NOT NULL,
    sub_code        VARCHAR(6)      NOT NULL,
    item_name       VARCHAR(30)     NOT NULL DEFAULT '',
    item_type       CHAR(1)         NULL,
    full_count      INT             NULL,
    unit_money      INT UNSIGNED    NULL,
    repay_amount    BIGINT          NULL,
    internal_comment VARCHAR(150)   NULL,
    secondary_comment VARCHAR(200)  NULL,
    is_on_sale      BOOLEAN         NOT NULL DEFAULT TRUE,
    is_usable       BOOLEAN         NOT NULL DEFAULT TRUE,
    age_limit       SMALLINT UNSIGNED NULL,
    sex_code        CHAR(1)         NULL,
    item_description VARCHAR(300)   NULL,
    give_resource   VARCHAR(20)     NULL,
    give_money_type CHAR(1)         NULL,
    function_box    VARCHAR(200)    NULL,
    is_client_only  BOOLEAN         NULL,
    is_used_on_purchase BOOLEAN     NULL,
    max_purchase_count SMALLINT UNSIGNED NULL,
    available_days  INT             NULL,
    is_resellable   BOOLEAN         NULL,
    is_presentable  BOOLEAN         NULL,
    is_presentable_in_bag BOOLEAN   NULL,
    is_exposed      BOOLEAN         NULL,
    money_unit      CHAR(1)         NULL,
    av_code         VARCHAR(10)     NULL,
    modified_at     DATETIME(3)     NULL,
    PRIMARY KEY (item_code, sub_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: custom_item_master
CREATE TABLE custom_item_master (
    custom_id       INT UNSIGNED    NOT NULL,
    kind            TINYINT UNSIGNED NOT NULL DEFAULT 10,
    item_name       VARCHAR(80)     NOT NULL DEFAULT '',
    is_valid        BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (custom_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: custom_item_set
CREATE TABLE custom_item_set (
    set_id          INT UNSIGNED    NOT NULL,
    custom_id       INT UNSIGNED    NOT NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (set_id, custom_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: custom_shop_master
CREATE TABLE custom_shop_master (
    shop_no         INT UNSIGNED    NOT NULL,
    custom_id       INT UNSIGNED    NOT NULL,
    shop_name       VARCHAR(80)     NOT NULL DEFAULT '',
    description     TEXT            NULL,
    hc_price        INT UNSIGNED    NOT NULL DEFAULT 0,
    game_money      INT UNSIGNED    NOT NULL DEFAULT 0,
    av_code         VARCHAR(30)     NULL,
    sale_start_at   DATETIME(3)     NULL,
    sale_end_at     DATETIME(3)     NULL,
    is_valid        BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (shop_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: cup_master
CREATE TABLE cup_master (
    cup_id              INT UNSIGNED    NOT NULL,
    cup_name            VARCHAR(40)     NOT NULL DEFAULT '',
    short_cup_name      VARCHAR(12)     NOT NULL DEFAULT '',
    rule_id             SMALLINT UNSIGNED NOT NULL DEFAULT 1,
    condition_match_count SMALLINT         NOT NULL DEFAULT -1,
    condition_regular   TINYINT UNSIGNED NOT NULL DEFAULT 0,
    start_at            DATETIME(3)     NOT NULL,
    end_at              DATETIME(3)     NOT NULL,
    nickname_start_at   DATETIME(3)     NOT NULL,
    nickname_end_at     DATETIME(3)     NOT NULL,
    prize               VARCHAR(1000)   NOT NULL DEFAULT '',
    detail              VARCHAR(1000)   NOT NULL DEFAULT '',
    status              TINYINT UNSIGNED NOT NULL DEFAULT 0,
    is_active           BOOLEAN         NOT NULL DEFAULT TRUE,
    PRIMARY KEY (cup_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: cup_channel
CREATE TABLE cup_channel (
    cup_id          INT UNSIGNED    NOT NULL,
    channel_id      VARCHAR(30)     NOT NULL,
    PRIMARY KEY (cup_id, channel_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_plan
CREATE TABLE tournament_plan (
    cup_id          INT UNSIGNED    NOT NULL,
    seq             INT UNSIGNED    NOT NULL DEFAULT 1,
    cup_name        VARCHAR(40)     NOT NULL DEFAULT '',
    is_final        BOOLEAN         NOT NULL DEFAULT FALSE,
    start_at        DATETIME(3)     NOT NULL,
    end_at          DATETIME(3)     NOT NULL,
    min_level       TINYINT UNSIGNED NOT NULL DEFAULT 0,
    max_level       TINYINT UNSIGNED NOT NULL DEFAULT 10,
    unit_money      INT             NOT NULL DEFAULT 0,
    max_match_count SMALLINT        NOT NULL DEFAULT -1,
    min_match_count SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    prize           VARCHAR(1000)   NOT NULL DEFAULT '',
    detail          VARCHAR(1000)   NOT NULL DEFAULT '',
    status          TINYINT UNSIGNED NOT NULL DEFAULT 0,
    admin_comment   VARCHAR(255)    NOT NULL DEFAULT '',
    is_valid        BOOLEAN         NOT NULL DEFAULT TRUE,
    rule_id         SMALLINT UNSIGNED NOT NULL DEFAULT 1,
    notice_url      VARCHAR(255)    NOT NULL DEFAULT '',
    banner_url      VARCHAR(255)    NOT NULL DEFAULT '',
    billing_status  TINYINT UNSIGNED NOT NULL DEFAULT 0,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (cup_id, seq),
    INDEX idx_tournament_plan_status (status, start_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_limit
CREATE TABLE tournament_limit (
    limit_no        TINYINT UNSIGNED NOT NULL,
    is_valid        BOOLEAN         NOT NULL DEFAULT FALSE,
    limit_start_at  DATETIME(3)     NOT NULL,
    limit_end_at    DATETIME(3)     NOT NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (limit_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: grade_rank_schedule
CREATE TABLE grade_rank_schedule (
    rank_date       DATE            NOT NULL,
    batch_flag      TINYINT UNSIGNED NOT NULL DEFAULT 0,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (rank_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_daily_mission
CREATE TABLE player_daily_mission (
    member_no          BIGINT UNSIGNED NOT NULL,
    mission_id      TINYINT UNSIGNED NOT NULL,
    progress_count  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    mission_state   TINYINT UNSIGNED NOT NULL DEFAULT 0,

    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, mission_id),
    CONSTRAINT chk_player_daily_mission_state
        CHECK (mission_state IN (0, 1, 2))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_weekly_reward
CREATE TABLE player_weekly_reward (
    member_no          BIGINT UNSIGNED NOT NULL,
    reward_week     DATE            NOT NULL,
    reward_id       TINYINT UNSIGNED NOT NULL,
    receive_status  TINYINT UNSIGNED NOT NULL DEFAULT 0,

    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, reward_week, reward_id),
    CONSTRAINT chk_player_weekly_reward_status
        CHECK (receive_status IN (0, 1, 2))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_title
CREATE TABLE player_title (
    member_no          BIGINT UNSIGNED NOT NULL,
    title_id        VARCHAR(10)     NOT NULL,
    valid_flag      CHAR(1)         NULL,
    acquired_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, title_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_function_item
CREATE TABLE player_function_item (
    member_no          BIGINT UNSIGNED NOT NULL,
    item_code       VARCHAR(10)     NOT NULL,
    quantity        INT UNSIGNED    NOT NULL DEFAULT 1,
    bought_at       DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    expires_at      DATETIME(3)     NULL,
    is_equipped     BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, item_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_custom_item
CREATE TABLE player_custom_item (
    member_no          BIGINT UNSIGNED NOT NULL,
    custom_id       INT UNSIGNED    NOT NULL,
    quantity        SMALLINT UNSIGNED NOT NULL DEFAULT 1,
    equip_slot      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    acquired_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, custom_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_present
CREATE TABLE player_present (
    present_id      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    member_no          BIGINT UNSIGNED NOT NULL,
    receive_status  TINYINT UNSIGNED NOT NULL DEFAULT 0,
    present_amount  BIGINT          NOT NULL DEFAULT 0,
    present_type    TINYINT UNSIGNED NOT NULL,

    present_kind    TINYINT UNSIGNED NOT NULL,
    present_info    VARCHAR(200)    NULL,
    present_ref_id  VARCHAR(20)     NULL,
    expires_at      DATETIME(3)     NULL,
    sent_at         DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    received_at     DATETIME(3)     NULL,
    PRIMARY KEY (present_id),
    INDEX idx_player_present_member_status (member_no, receive_status),
    CONSTRAINT chk_player_present_status
        CHECK (receive_status IN (0, 1, 2))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_grade_rank
CREATE TABLE player_grade_rank (
    rank_date       DATE            NOT NULL,
    rank_kind       TINYINT UNSIGNED NOT NULL DEFAULT 0,

    member_no          BIGINT UNSIGNED NOT NULL,
    rating          INT             NOT NULL DEFAULT 1500,
    grade_level     INT             NOT NULL DEFAULT 0,
    last_played_at  DATETIME(3)     NULL,
    extra_count     INT UNSIGNED    NOT NULL DEFAULT 0,
    last_extra_at   DATETIME(3)     NULL,
    avatar_id       VARCHAR(200)    NOT NULL DEFAULT '',
    display_flag    TINYINT UNSIGNED NOT NULL DEFAULT 0,
    rank_position   INT             NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (rank_date, rank_kind, member_no),
    INDEX idx_player_grade_rank_date_kind_rating (rank_date, rank_kind, rating DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_yaku_stats
CREATE TABLE player_yaku_stats (
    member_no          BIGINT UNSIGNED NOT NULL,

    y_haitei        INT UNSIGNED NOT NULL DEFAULT 0,
    y_houtei        INT UNSIGNED NOT NULL DEFAULT 0,
    y_rinshan       INT UNSIGNED NOT NULL DEFAULT 0,
    y_tsumo         INT UNSIGNED NOT NULL DEFAULT 0,
    y_richi         INT UNSIGNED NOT NULL DEFAULT 0,
    y_ippatsu       INT UNSIGNED NOT NULL DEFAULT 0,
    y_yakuhai       INT UNSIGNED NOT NULL DEFAULT 0,
    y_pinfu         INT UNSIGNED NOT NULL DEFAULT 0,
    y_tanyao        INT UNSIGNED NOT NULL DEFAULT 0,
    y_iipeikou      INT UNSIGNED NOT NULL DEFAULT 0,
    y_chitoitsu     INT UNSIGNED NOT NULL DEFAULT 0,
    y_ittsuu        INT UNSIGNED NOT NULL DEFAULT 0,
    y_toitoi        INT UNSIGNED NOT NULL DEFAULT 0,
    y_sanshoku_jun  INT UNSIGNED NOT NULL DEFAULT 0,
    y_sanshoku_seq  INT UNSIGNED NOT NULL DEFAULT 0,
    y_sanshoku_kou  INT UNSIGNED NOT NULL DEFAULT 0,
    y_chankan       INT UNSIGNED NOT NULL DEFAULT 0,
    y_sanankou      INT UNSIGNED NOT NULL DEFAULT 0,
    y_sankantsu     INT UNSIGNED NOT NULL DEFAULT 0,
    y_shosangen     INT UNSIGNED NOT NULL DEFAULT 0,
    y_honroutou     INT UNSIGNED NOT NULL DEFAULT 0,
    y_chanta        INT UNSIGNED NOT NULL DEFAULT 0,
    y_junchan       INT UNSIGNED NOT NULL DEFAULT 0,
    y_ryanpeikou    INT UNSIGNED NOT NULL DEFAULT 0,
    y_honisou       INT UNSIGNED NOT NULL DEFAULT 0,
    y_chinisou      INT UNSIGNED NOT NULL DEFAULT 0,
    y_wrichi        INT UNSIGNED NOT NULL DEFAULT 0,
    y_dora          INT UNSIGNED NOT NULL DEFAULT 0,

    y_daisangen     INT UNSIGNED NOT NULL DEFAULT 0,
    y_suuankou      INT UNSIGNED NOT NULL DEFAULT 0,
    y_suukantsu     INT UNSIGNED NOT NULL DEFAULT 0,
    y_shosuushi     INT UNSIGNED NOT NULL DEFAULT 0,
    y_chinroutou    INT UNSIGNED NOT NULL DEFAULT 0,
    y_tsuisou       INT UNSIGNED NOT NULL DEFAULT 0,
    y_ryuisou       INT UNSIGNED NOT NULL DEFAULT 0,
    y_churenpaotou  INT UNSIGNED NOT NULL DEFAULT 0,
    y_kokushi       INT UNSIGNED NOT NULL DEFAULT 0,
    y_tenhou        INT UNSIGNED NOT NULL DEFAULT 0,
    y_chihou        INT UNSIGNED NOT NULL DEFAULT 0,

    y_suuankou2     INT UNSIGNED NOT NULL DEFAULT 0,
    y_daisuushi     INT UNSIGNED NOT NULL DEFAULT 0,
    y_kokushi2      INT UNSIGNED NOT NULL DEFAULT 0,
    y_churenpaotou2 INT UNSIGNED NOT NULL DEFAULT 0,

    cont_top_max    INT UNSIGNED NOT NULL DEFAULT 0,
    cont_top_now    INT UNSIGNED NOT NULL DEFAULT 0,
    score_max       INT          NOT NULL DEFAULT 0,
    money_max       BIGINT       NOT NULL DEFAULT 0,
    hora_dora_max   INT UNSIGNED NOT NULL DEFAULT 0,
    cont_last_max   INT UNSIGNED NOT NULL DEFAULT 0,
    cont_last_now   INT UNSIGNED NOT NULL DEFAULT 0,
    score_min       INT          NOT NULL DEFAULT 0,
    money_min       BIGINT       NOT NULL DEFAULT 0,
    updated_at      DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                 ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: cup_player_rating
CREATE TABLE cup_player_rating (
    cup_id          INT UNSIGNED    NOT NULL,
    member_no          BIGINT UNSIGNED NOT NULL,
    cup_point       INT             NOT NULL DEFAULT 0,
    match_count     SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    joined_at       DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    last_played_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (cup_id, member_no),
    INDEX idx_cup_player_rating_cup_point (cup_id, cup_point DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_player_rating
CREATE TABLE tournament_player_rating (
    cup_id          INT UNSIGNED    NOT NULL,
    seq             INT UNSIGNED    NOT NULL DEFAULT 1,
    member_no          BIGINT UNSIGNED NOT NULL,
    total_point     BIGINT          NOT NULL DEFAULT 0,
    match_count     SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    point_slot_1    BIGINT          NULL,
    point_slot_2    BIGINT          NULL,
    point_slot_3    BIGINT          NULL,
    point_slot_4    BIGINT          NULL,
    point_slot_5    BIGINT          NULL,
    point_slot_6    BIGINT          NULL,
    point_slot_7    BIGINT          NULL,
    bought_at       DATETIME(3)     NULL,
    joined_at       DATETIME(3)     NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (cup_id, seq, member_no),
    INDEX idx_tournament_player_rating_cupseq_point (cup_id, seq, total_point DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_session
CREATE TABLE tournament_session (
    session_id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    join_start_at       DATETIME(3)     NOT NULL,
    match_start_at      DATETIME(3)     NOT NULL,
    play_start_at       DATETIME(3)     NOT NULL,
    play_end_at         DATETIME(3)     NOT NULL,
    view_end_at         DATETIME(3)     NOT NULL,
    next_start_at       DATETIME(3)     NOT NULL,
    next_cut_at         DATETIME(3)     NOT NULL,
    play_schedule       VARCHAR(200)    NOT NULL,
    play_status         TINYINT UNSIGNED NOT NULL DEFAULT 0,
    play_phase          TINYINT UNSIGNED NOT NULL DEFAULT 0,
    player_count        SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    max_player_count    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    max_room_count      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    session_name        VARCHAR(100)    NOT NULL DEFAULT '',
    room_option         VARCHAR(20)     NOT NULL DEFAULT '',
    private_info        VARCHAR(20)     NULL,
    max_viewer_count    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    play_count          TINYINT UNSIGNED NOT NULL DEFAULT 0,
    play_time           TINYINT UNSIGNED NOT NULL DEFAULT 0,
    play_mode           TINYINT UNSIGNED NOT NULL DEFAULT 0,
    join_money          BIGINT          NOT NULL DEFAULT 0,
    prize_money_1       BIGINT          NOT NULL DEFAULT 0,
    prize_money_2       BIGINT          NOT NULL DEFAULT 0,
    prize_money_3       BIGINT          NOT NULL DEFAULT 0,
    prize_money_4       BIGINT          NOT NULL DEFAULT 0,
    plan_member_no          BIGINT UNSIGNED NULL,
    result_member_no_1     BIGINT UNSIGNED NULL,
    result_member_no_2     BIGINT UNSIGNED NULL,
    result_member_no_3     BIGINT UNSIGNED NULL,
    result_member_no_4     BIGINT UNSIGNED NULL,
    created_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                        ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (session_id),
    INDEX idx_tournament_session_status (play_status, player_count, max_player_count)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_participant
CREATE TABLE tournament_participant (
    member_no          BIGINT UNSIGNED NOT NULL,
    session_id          BIGINT UNSIGNED NOT NULL,
    join_seq_no         BIGINT UNSIGNED NOT NULL,
    join_member_no      CHAR(3)         NOT NULL,
    join_status         TINYINT UNSIGNED NOT NULL DEFAULT 0,
    total_manage_count  INT UNSIGNED    NOT NULL DEFAULT 0,
    manage_count        INT UNSIGNED    NOT NULL DEFAULT 0,
    last_manage_at      DATETIME(3)     NULL,
    created_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at          DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                        ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no),
    INDEX idx_tournament_participant_member_status (member_no, join_status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: tournament_room
CREATE TABLE tournament_room (
    session_id      BIGINT UNSIGNED NOT NULL,
    sub_id          SMALLINT UNSIGNED NOT NULL,
    room_id         SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    plan_start_at   DATETIME(3)     NOT NULL,
    plan_end_at     DATETIME(3)     NOT NULL,
    started_at      DATETIME(3)     NULL,
    ended_at        DATETIME(3)     NULL,
    member_no_1     BIGINT UNSIGNED NULL,
    member_no_2     BIGINT UNSIGNED NULL,
    member_no_3     BIGINT UNSIGNED NULL,
    member_no_4     BIGINT UNSIGNED NULL,
    join_member_no_1 CHAR(3)         NULL,
    join_member_no_2 CHAR(3)         NULL,
    join_member_no_3 CHAR(3)         NULL,
    join_member_no_4 CHAR(3)         NULL,
    score_tmp_1     INT             NOT NULL DEFAULT 0,
    score_tmp_2     INT             NOT NULL DEFAULT 0,
    score_tmp_3     INT             NOT NULL DEFAULT 0,
    score_tmp_4     INT             NOT NULL DEFAULT 0,
    score_1         INT             NOT NULL DEFAULT 0,
    score_2         INT             NOT NULL DEFAULT 0,
    score_3         INT             NOT NULL DEFAULT 0,
    score_4         INT             NOT NULL DEFAULT 0,
    rank1_member_no          BIGINT UNSIGNED NULL,
    rank2_member_no          BIGINT UNSIGNED NULL,
    rank3_member_no          BIGINT UNSIGNED NULL,
    rank4_member_no          BIGINT UNSIGNED NULL,
    grade1_member_no CHAR(3)         NULL,
    grade2_member_no CHAR(3)         NULL,
    grade3_member_no CHAR(3)         NULL,
    grade4_member_no CHAR(3)         NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (session_id, sub_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: channel_runtime
CREATE TABLE channel_runtime (
    channel_id      VARCHAR(30)     NOT NULL,
    game_id         VARCHAR(10)     NOT NULL DEFAULT 'MAJAK4',
    sub_id          VARCHAR(5)      NOT NULL DEFAULT '',
    go_service      VARCHAR(30)     NOT NULL DEFAULT '',
    server_ip       VARCHAR(50)     NOT NULL DEFAULT '',
    server_port     MEDIUMINT UNSIGNED NOT NULL DEFAULT 0,
    game_port       MEDIUMINT UNSIGNED NOT NULL DEFAULT 0,
    query_port      MEDIUMINT UNSIGNED NOT NULL DEFAULT 0,
    channel_name    VARCHAR(50)     NOT NULL DEFAULT '',
    max_member      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    max_room        SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    unit_money      INT UNSIGNED    NOT NULL DEFAULT 0,
    member_count    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    used_room       SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    item_yes_count  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    item_no_count   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    member_male     SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    member_female   SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    machine_name    VARCHAR(20)     NOT NULL DEFAULT '',
    channel_server_version DATETIME(3) NULL,
    room_server_version DATETIME(3) NULL,
    last_seen_at    DATETIME(3)     NULL,
    zone_id         VARCHAR(3)      NOT NULL DEFAULT 'JPN',
    scope           CHAR(1)         NOT NULL DEFAULT 'Z',
    service_mask    TINYINT UNSIGNED NOT NULL DEFAULT 0,
    is_locked       BOOLEAN         NOT NULL DEFAULT FALSE,
    description     VARCHAR(128)    NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (channel_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: event_master
CREATE TABLE event_master (
    event_code      VARCHAR(10)     NOT NULL,
    event_no        INT UNSIGNED    NOT NULL DEFAULT 0,
    event_name      VARCHAR(120)    NOT NULL DEFAULT '',
    description     VARCHAR(1000)   NOT NULL DEFAULT '',
    service_id      VARCHAR(20)     NOT NULL DEFAULT 'MAJAK4',
    table_info      VARCHAR(100)    NOT NULL DEFAULT '',
    starts_at       DATETIME(3)     NULL,
    ends_at         DATETIME(3)     NULL,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (event_code, event_no),
    INDEX idx_event_master_active (service_id, starts_at, ends_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: event_user
CREATE TABLE event_user (
    event_code          VARCHAR(10)     NOT NULL,
    event_no            INT UNSIGNED    NOT NULL DEFAULT 0,
    member_no          BIGINT UNSIGNED NOT NULL,
    total_earned_point  BIGINT          NOT NULL DEFAULT 0,
    daily_earned_point  BIGINT          NOT NULL DEFAULT 0,
    total_used_point    BIGINT          NOT NULL DEFAULT 0,
    last_activity_at    DATETIME(3)     NULL,
    registered_at       DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    extra_value1        BIGINT          NOT NULL DEFAULT 0,
    extra_value2        BIGINT          NOT NULL DEFAULT 0,
    extra_value3        BIGINT          NOT NULL DEFAULT 0,
    extra_value4        BIGINT          NOT NULL DEFAULT 0,
    extra_value5        BIGINT          NOT NULL DEFAULT 0,
    extra_value6        BIGINT          NOT NULL DEFAULT 0,
    extra_value7        BIGINT          NOT NULL DEFAULT 0,
    extra_info1         VARCHAR(150)    NOT NULL DEFAULT '',
    extra_info2         VARCHAR(150)    NOT NULL DEFAULT '',
    extra_info3         VARCHAR(500)    NOT NULL DEFAULT '',
    extra_info4         VARCHAR(500)    NOT NULL DEFAULT '',
    PRIMARY KEY (event_code, event_no, member_no),
    INDEX idx_event_user_member (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: game_admin_member
CREATE TABLE game_admin_member (
    member_no          BIGINT UNSIGNED NOT NULL,
    admin_status    INT UNSIGNED    NOT NULL DEFAULT 0,
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                    ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_avatar_inventory
CREATE TABLE player_avatar_inventory (
    inventory_id   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    member_no          BIGINT UNSIGNED NOT NULL,
    avatar_code    VARCHAR(32)     NOT NULL,
    cost_money     BIGINT          NOT NULL DEFAULT 0,
    cost_gem       INT             NOT NULL DEFAULT 0,
    acquired_at    DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (inventory_id),
    INDEX idx_player_avatar_inventory_member (member_no, acquired_at),
    INDEX idx_player_avatar_inventory_code (member_no, avatar_code),
    CONSTRAINT chk_player_avatar_inventory_cost
        CHECK (cost_money >= 0 AND cost_gem >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_daily_mission_history
CREATE TABLE player_daily_mission_history (
    member_no          BIGINT UNSIGNED NOT NULL,
    target_date     DATE             NOT NULL,
    mission_id      TINYINT UNSIGNED NOT NULL,
    progress_count  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    mission_state   TINYINT UNSIGNED NOT NULL DEFAULT 0,
    created_at      DATETIME(3)      NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)      NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                     ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, target_date, mission_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_skin
CREATE TABLE player_skin (
    member_no          BIGINT UNSIGNED NOT NULL,
    skin_no         SMALLINT UNSIGNED NOT NULL,
    is_attached     BOOLEAN     NOT NULL DEFAULT FALSE,
    expires_at      DATETIME(3) NOT NULL,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, skin_no),
    INDEX idx_player_skin_expiry (member_no, expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_shop
CREATE TABLE player_shop (
    member_no          BIGINT UNSIGNED NOT NULL,
    shop_id         SMALLINT UNSIGNED NOT NULL,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    opened_at       DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (member_no, shop_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: memorial_shop_master
CREATE TABLE memorial_shop_master (
    shop_id         SMALLINT UNSIGNED NOT NULL,
    shop_name       VARCHAR(20)       NOT NULL,
    created_at      DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                      ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (shop_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: event_gift_master
CREATE TABLE event_gift_master (
    event_code      VARCHAR(20) NOT NULL,
    event_no        INT UNSIGNED NOT NULL,
    gift_code       VARCHAR(20) NOT NULL,
    gift_name       VARCHAR(100) NULL,
    gift_value      BIGINT      NULL,
    gift_type       CHAR(1)     NULL,
    total_limit_count INT UNSIGNED NULL,
    daily_limit_count INT UNSIGNED NULL,
    mission_no      INT         NOT NULL DEFAULT 0,
    gift_message    VARCHAR(500) NULL,
    gift_avatar_id  VARCHAR(300) NULL,
    gift_group      VARCHAR(10) NULL,
    gift_sender_id  VARCHAR(20) NULL,
    created_at      DATETIME(3) NULL,
    updated_at      DATETIME(3) NULL,
    PRIMARY KEY (event_code, event_no, gift_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: serial_exchange_item
CREATE TABLE serial_exchange_item (
    event_code      VARCHAR(20) NOT NULL,
    event_no        INT UNSIGNED NOT NULL,
    service_id      VARCHAR(20) NOT NULL,
    member_no          BIGINT UNSIGNED NOT NULL,
    gift_code       VARCHAR(20) NOT NULL,
    gift_value      BIGINT      NOT NULL DEFAULT 0,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (event_code, event_no, service_id, member_no, gift_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: serial_coupon
CREATE TABLE serial_coupon (
    event_code      VARCHAR(20) NOT NULL,
    event_no        INT UNSIGNED NOT NULL,
    mission_no      INT         NOT NULL,
    coupon_no       VARCHAR(100) NOT NULL,
    inquiry_check_no VARCHAR(30) NULL,
    gift_code       VARCHAR(20) NULL,
    inquiry_comment VARCHAR(400) NULL,
    valid_check     CHAR(1) NULL,
    member_no          BIGINT UNSIGNED NULL,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (event_code, event_no, mission_no, coupon_no),
    INDEX idx_serial_coupon_member (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: game_clear_count
CREATE TABLE game_clear_count (
    game_id         VARCHAR(20) NOT NULL,
    game_description VARCHAR(256) NULL,
    count_description VARCHAR(256) NULL,
    count_image_url VARCHAR(256) NULL,
    clear_count     BIGINT      NOT NULL DEFAULT 0,
    admin_no        BIGINT UNSIGNED NULL,
    count_status    TINYINT UNSIGNED NOT NULL DEFAULT 0,
    is_valid        BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    updated_at      DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                                ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (game_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

