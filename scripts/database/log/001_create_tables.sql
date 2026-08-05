-- MySQL 8.0 以上
-- 対象データベース: majak_log
-- ログデータベース の新規構築用基準スキーマ
-- 外部キー制約は使用しない

-- テーブル: game_session_log
CREATE TABLE game_session_log (
    game_session_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    played_at       DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    channel_id      VARCHAR(30) NOT NULL,
    room_id         INT UNSIGNED NOT NULL,
    is_private      BOOLEAN NOT NULL DEFAULT FALSE,
    room_option     VARCHAR(200) NOT NULL DEFAULT '',
    money_rate      BIGINT NOT NULL DEFAULT 0,
    minimum_money   BIGINT NOT NULL DEFAULT 0,
    maximum_money   BIGINT NOT NULL DEFAULT 0,
    minimum_class   TINYINT UNSIGNED NULL,
    maximum_class   TINYINT UNSIGNED NULL,
    cup_id          BIGINT UNSIGNED NULL,
    rule_id         SMALLINT UNSIGNED NULL,
    cup_sequence    BIGINT UNSIGNED NULL,
    used_ticket     SMALLINT UNSIGNED NULL,
    cup_rule        TINYINT UNSIGNED NULL,
    PRIMARY KEY (game_session_id),
    INDEX idx_game_session_log_played_at (played_at),
    INDEX idx_game_session_log_channel_played (channel_id, played_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: game_player_result_log
CREATE TABLE game_player_result_log (
    game_player_result_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    game_session_id       BIGINT UNSIGNED NOT NULL,
    played_at             DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no             BIGINT UNSIGNED NOT NULL,
    was_connected         BOOLEAN NOT NULL DEFAULT TRUE,
    ranking               TINYINT UNSIGNED NOT NULL,
    score                 INT NOT NULL DEFAULT 0,
    point                 INT NOT NULL DEFAULT 0,
    had_yakitori          BOOLEAN NOT NULL DEFAULT FALSE,
    chip                  INT NOT NULL DEFAULT 0,
    money_before          BIGINT NOT NULL DEFAULT 0,
    lent_money_before     BIGINT NOT NULL DEFAULT 0,
    dealer_fee            BIGINT NOT NULL DEFAULT 0,
    money_change          BIGINT NOT NULL DEFAULT 0,
    money_after           BIGINT NOT NULL DEFAULT 0,
    lent_money_after      BIGINT NOT NULL DEFAULT 0,
    ip_address            VARCHAR(45) NOT NULL DEFAULT '',
    gateway               VARCHAR(45) NOT NULL DEFAULT '',
    mac_address           VARCHAR(17) NOT NULL DEFAULT '',
    previous_ticket       BIGINT NULL,
    returned_ticket       BIGINT NULL,
    previous_class        TINYINT UNSIGNED NULL,
    current_class         TINYINT UNSIGNED NULL,
    current_ticket        BIGINT NULL,
    PRIMARY KEY (game_player_result_id),
    UNIQUE KEY uq_game_player_result_session_member (game_session_id, member_no),
    INDEX idx_game_player_result_member_played (member_no, played_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: training_session_log
CREATE TABLE training_session_log (
    training_session_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    played_at           DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    channel_id          VARCHAR(30) NOT NULL,
    room_id             INT UNSIGNED NOT NULL,
    room_option         VARCHAR(200) NOT NULL DEFAULT '',
    player_count        TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (training_session_id),
    INDEX idx_training_session_log_played_at (played_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: training_player_result_log
CREATE TABLE training_player_result_log (
    training_player_result_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    training_session_id       BIGINT UNSIGNED NOT NULL,
    seat_order                TINYINT UNSIGNED NOT NULL,
    member_no                 BIGINT UNSIGNED NULL,
    point                     INT NOT NULL DEFAULT 0,
    PRIMARY KEY (training_player_result_id),
    UNIQUE KEY uq_training_player_result_seat (training_session_id, seat_order),
    INDEX idx_training_player_result_member (member_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: weekly_reward_claim_log
CREATE TABLE weekly_reward_claim_log (
    weekly_reward_claim_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    member_no              BIGINT UNSIGNED NOT NULL,
    reward_week            DATE NOT NULL,
    reward_id              INT UNSIGNED NOT NULL,
    receive_status         TINYINT UNSIGNED NOT NULL,
    claimed_at             DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (weekly_reward_claim_id),
    UNIQUE KEY uq_weekly_reward_claim (member_no, reward_week, reward_id),
    INDEX idx_weekly_reward_claim_week (reward_week, claimed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: money_transaction_log
CREATE TABLE money_transaction_log (
    money_transaction_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    occurred_at           DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no             BIGINT UNSIGNED NOT NULL,
    event_code            VARCHAR(32) NOT NULL,
    event_title           VARCHAR(100) NOT NULL DEFAULT '',
    game_id               VARCHAR(20) NOT NULL DEFAULT 'MAJAK4',
    amount                BIGINT NOT NULL,
    balance_before        BIGINT NOT NULL,
    balance_after         BIGINT NOT NULL,
    is_valid              BOOLEAN NOT NULL DEFAULT TRUE,
    order_number          VARCHAR(64) NULL,
    additional_info       VARCHAR(100) NULL,
    billing_order_number  VARCHAR(20) NULL,
    unit_count            INT UNSIGNED NOT NULL DEFAULT 1,
    remote_address        VARCHAR(45) NOT NULL DEFAULT '',
    PRIMARY KEY (money_transaction_id),
    INDEX idx_money_transaction_member_occurred (member_no, occurred_at),
    INDEX idx_money_transaction_event_occurred (event_code, occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: winning_yaku_log
CREATE TABLE winning_yaku_log (
    winning_yaku_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    occurred_at         DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no           BIGINT UNSIGNED NOT NULL,
    game_id             VARCHAR(20) NOT NULL DEFAULT 'MAJAK4',
    yaku_code           INT NOT NULL,
    PRIMARY KEY (winning_yaku_log_id),
    INDEX idx_winning_yaku_member_occurred (member_no, occurred_at),
    INDEX idx_winning_yaku_code_occurred (yaku_code, occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: item_purchase_log
CREATE TABLE item_purchase_log (
    item_purchase_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    purchased_at     DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no        BIGINT UNSIGNED NOT NULL,
    item_code        VARCHAR(64) NOT NULL,
    quantity         INT UNSIGNED NOT NULL DEFAULT 1,
    unit_price       BIGINT NOT NULL DEFAULT 0,
    external_user_no VARCHAR(64) NULL,
    purchase_channel INT UNSIGNED NOT NULL DEFAULT 2,
    order_number     VARCHAR(64) NULL,
    PRIMARY KEY (item_purchase_id),
    INDEX idx_item_purchase_member_purchased (member_no, purchased_at),
    INDEX idx_item_purchase_item_purchased (item_code, purchased_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: cash_transaction_log
CREATE TABLE cash_transaction_log (
    id              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    member_no       BIGINT UNSIGNED NOT NULL,
    event_type      VARCHAR(30)   NOT NULL,

    amount          INT           NOT NULL,
    balance_before  INT UNSIGNED  NOT NULL,
    balance_after   INT UNSIGNED  NOT NULL,
    paid_amount     INT           NOT NULL DEFAULT 0,
    free_amount     INT           NOT NULL DEFAULT 0,
    paid_before     INT UNSIGNED  NOT NULL DEFAULT 0,
    paid_after      INT UNSIGNED  NOT NULL DEFAULT 0,
    free_before     INT UNSIGNED  NOT NULL DEFAULT 0,
    free_after      INT UNSIGNED  NOT NULL DEFAULT 0,
    ref_id          VARCHAR(64)   NULL,
    memo            VARCHAR(200)  NULL,
    operator_no     BIGINT UNSIGNED NULL,
    client_ip       VARCHAR(45)   NULL,
    occurred_at     DATETIME(3)   NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    INDEX idx_cash_tx_member  (member_no, occurred_at),
    INDEX idx_cash_tx_type    (event_type, occurred_at),
    INDEX idx_cash_tx_ref     (ref_id),
    INDEX idx_cash_tx_date    (occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: admin_operation_log
CREATE TABLE admin_operation_log (
    id              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    operator_no     BIGINT UNSIGNED NOT NULL,
    operator_role   VARCHAR(20)   NOT NULL,
    action          VARCHAR(50)   NOT NULL,
    target_type     VARCHAR(50)   NULL,
    target_id       VARCHAR(100)  NULL,
    payload_before  JSON          NULL,
    payload_after   JSON          NULL,
    memo            VARCHAR(500)  NULL,
    client_ip       VARCHAR(45)   NULL,
    occurred_at     DATETIME(3)   NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    INDEX idx_admin_op_operator (operator_no, occurred_at),
    INDEX idx_admin_op_target   (target_type, target_id),
    INDEX idx_admin_op_date     (occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: player_login_log
CREATE TABLE player_login_log (
    login_log_id    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    occurred_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no       BIGINT UNSIGNED NOT NULL,
    event_type      TINYINT UNSIGNED NOT NULL DEFAULT 0,

    ip_address      VARCHAR(45)     NOT NULL DEFAULT '',
    user_agent      VARCHAR(200)    NOT NULL DEFAULT '',
    PRIMARY KEY (login_log_id, occurred_at),
    INDEX idx_player_login_member_occurred (member_no, occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci

  PARTITION BY RANGE (TO_DAYS(occurred_at)) (
    PARTITION p_before_2025 VALUES LESS THAN (TO_DAYS('2025-01-01')),
    PARTITION p_2025        VALUES LESS THAN (TO_DAYS('2026-01-01')),
    PARTITION p_2026        VALUES LESS THAN (TO_DAYS('2027-01-01')),
    PARTITION p_future      VALUES LESS THAN MAXVALUE
  );

-- テーブル: daily_mission_completion_log
CREATE TABLE daily_mission_completion_log (
    completion_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    member_no         BIGINT UNSIGNED NOT NULL,
    target_date       DATE            NOT NULL,
    mission_id        TINYINT UNSIGNED NOT NULL,
    progress_count    SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    mission_state     TINYINT UNSIGNED NOT NULL DEFAULT 0,

    completed_at      DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (completion_log_id),
    UNIQUE KEY uq_daily_mission_completion (member_no, target_date, mission_id),
    INDEX idx_daily_mission_completion_date (target_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: custom_item_purchase_log
CREATE TABLE custom_item_purchase_log (
    purchase_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    occurred_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no       BIGINT UNSIGNED NOT NULL,
    shop_no         INT UNSIGNED    NULL,
    custom_id       INT UNSIGNED    NOT NULL,
    source_type     TINYINT UNSIGNED NOT NULL DEFAULT 1,
    gem_price       INT             NOT NULL DEFAULT 0,
    hc_price        INT             NOT NULL DEFAULT 0,
    game_money      INT             NOT NULL DEFAULT 0,
    order_id        VARCHAR(64)     NULL,
    PRIMARY KEY (purchase_log_id),
    INDEX idx_custom_item_purchase_member (member_no, occurred_at),
    INDEX idx_custom_item_purchase_item (custom_id, occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: present_delivery_log
CREATE TABLE present_delivery_log (
    delivery_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    occurred_at     DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    member_no       BIGINT UNSIGNED NOT NULL,
    present_id      BIGINT UNSIGNED NOT NULL,
    event_type      TINYINT UNSIGNED NOT NULL DEFAULT 0,

    present_type    TINYINT UNSIGNED NOT NULL,
    present_amount  BIGINT          NOT NULL DEFAULT 0,
    admin_email     VARCHAR(254)    NULL,
    reason          VARCHAR(200)    NOT NULL DEFAULT '',
    PRIMARY KEY (delivery_log_id),
    INDEX idx_present_delivery_member (member_no, occurred_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: grade_rank_snapshot_log
CREATE TABLE grade_rank_snapshot_log (
    snapshot_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    snapshot_date   DATE            NOT NULL,
    rank_kind       TINYINT UNSIGNED NOT NULL DEFAULT 0,
    member_no       BIGINT UNSIGNED NOT NULL,
    rating          INT             NOT NULL DEFAULT 1500,
    grade_level     INT             NOT NULL DEFAULT 0,
    rank_position   INT             NULL,
    snapshotted_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    PRIMARY KEY (snapshot_log_id),
    INDEX idx_grade_rank_snapshot_date (snapshot_date, rank_kind, rank_position)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- テーブル: cup_match_log
CREATE TABLE cup_match_log (
    cup_match_log_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    played_at        DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    cup_id           INT UNSIGNED    NOT NULL,
    game_session_id  BIGINT UNSIGNED NULL,
    member_no        BIGINT UNSIGNED NOT NULL,
    ranking          TINYINT UNSIGNED NOT NULL DEFAULT 1,
    point_change     INT             NOT NULL DEFAULT 0,
    point_after      INT             NOT NULL DEFAULT 0,
    PRIMARY KEY (cup_match_log_id),
    INDEX idx_cup_match_log_cup_member (cup_id, member_no, played_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
