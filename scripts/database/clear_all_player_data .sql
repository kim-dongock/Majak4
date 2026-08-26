-- MySQL 8.0+
-- All registered players are reset to their post-registration baseline.
--
-- Before executing:
--   1. Stop every MajakServer instance.
--   2. Clear Redis runtime/auth cache separately.
--   3. Back up majak_game and majak_log.
--
-- Preserved:
--   player_account, admin_account, game_admin_member, all master data,
--   game_economy_policy, and admin_operation_log.
--
-- Deleted/reset:
--   Player state, owned items, purchase/charge history, gameplay records,
--   player activity logs, and paifu metadata. S3 paifu objects are not deleted.


TRUNCATE TABLE player_mode_stats;
TRUNCATE TABLE player_wallet;
TRUNCATE TABLE player_profile;
TRUNCATE TABLE player_account;

TRUNCATE TABLE player_mode_stats;
TRUNCATE TABLE player_high_class_summary;
TRUNCATE TABLE player_high_class_yaku;
TRUNCATE TABLE player_daily_mission;
TRUNCATE TABLE player_weekly_reward;
TRUNCATE TABLE player_title;
TRUNCATE TABLE player_function_item;
TRUNCATE TABLE player_custom_item;
TRUNCATE TABLE player_present;
TRUNCATE TABLE player_grade_rank;
TRUNCATE TABLE player_yaku_stats;
TRUNCATE TABLE cup_player_rating;
TRUNCATE TABLE tournament_player_rating;
TRUNCATE TABLE tournament_participant;
TRUNCATE TABLE tournament_room;
TRUNCATE TABLE tournament_session;
TRUNCATE TABLE event_user;
TRUNCATE TABLE player_avatar_inventory;
TRUNCATE TABLE player_daily_mission_history;
TRUNCATE TABLE player_skin;
TRUNCATE TABLE player_shop;
TRUNCATE TABLE serial_exchange_item;
TRUNCATE TABLE channel_runtime;
TRUNCATE TABLE grade_rank_schedule;
TRUNCATE TABLE cash_charge_order;

TRUNCATE TABLE serial_coupon;
TRUNCATE TABLE game_clear_count;

TRUNCATE TABLE paifu_archive_member_log;
TRUNCATE TABLE paifu_archive_log;
TRUNCATE TABLE game_player_result_log;
TRUNCATE TABLE game_session_log;
TRUNCATE TABLE training_player_result_log;
TRUNCATE TABLE training_session_log;
TRUNCATE TABLE weekly_reward_claim_log;
TRUNCATE TABLE money_transaction_log;
TRUNCATE TABLE winning_yaku_log;
TRUNCATE TABLE item_purchase_log;
TRUNCATE TABLE cash_transaction_log;
TRUNCATE TABLE player_login_log;
TRUNCATE TABLE daily_mission_completion_log;
TRUNCATE TABLE custom_item_purchase_log;
TRUNCATE TABLE present_delivery_log;
TRUNCATE TABLE grade_rank_snapshot_log;
TRUNCATE TABLE cup_match_log;

-- Keep administrator security/audit records.
-- TRUNCATE TABLE admin_operation_log;

-- Verification report. All values except accounts/wallets/profiles should be zero.
SELECT 'player_account' AS table_name, COUNT(*) AS row_count FROM player_account
UNION ALL SELECT 'player_wallet', COUNT(*) FROM player_wallet
UNION ALL SELECT 'player_profile', COUNT(*) FROM player_profile
UNION ALL SELECT 'player_mode_stats', COUNT(*) FROM player_mode_stats
UNION ALL SELECT 'cash_charge_order', COUNT(*) FROM cash_charge_order
UNION ALL SELECT 'player_custom_item', COUNT(*) FROM player_custom_item
UNION ALL SELECT 'player_present', COUNT(*) FROM player_present
UNION ALL SELECT 'player_grade_rank', COUNT(*) FROM player_grade_rank;

SELECT COUNT(*) AS invalid_mp_wallet_count
FROM player_wallet
WHERE cash_count <> 0
   OR paid_cash_count <> 0
   OR free_cash_count <> 0
   OR cash_count <> paid_cash_count + free_cash_count;

SELECT 'game_session_log' AS table_name, COUNT(*) AS row_count FROM game_session_log
UNION ALL SELECT 'game_player_result_log', COUNT(*) FROM game_player_result_log
UNION ALL SELECT 'money_transaction_log', COUNT(*) FROM money_transaction_log
UNION ALL SELECT 'cash_transaction_log', COUNT(*) FROM cash_transaction_log
UNION ALL SELECT 'paifu_archive_log', COUNT(*) FROM paifu_archive_log;


