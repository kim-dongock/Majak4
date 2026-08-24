-- 既存 majak_game DB 用: 高レート場代無料券の龍珠消費履歴コード
INSERT INTO transaction_code_master
    (transaction_code, code_title, is_history_enabled, is_cumulative, open_status, start_date, content, service_code, service_name, is_service_enabled, game_id, registrant_name, planner_name, developer_name, direction_code, avatar_code)
VALUES
    ('JM00657', '高レート場代無料券龍珠', TRUE, TRUE, 'P', '2026-08-24', NULL, 'ITEM', '麻雀４', TRUE, 'MAJAK4', 'system', 'game', 'system', 'O', NULL)
ON DUPLICATE KEY UPDATE transaction_code = VALUES(transaction_code);