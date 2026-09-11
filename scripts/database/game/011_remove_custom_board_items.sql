-- Remove all obsolete custom board items now that board color is user-selected.
-- Run once against the majak_game database. Purchase logs are intentionally retained for auditing.

START TRANSACTION;

CREATE TEMPORARY TABLE removed_board_item_ids (
    custom_id INT UNSIGNED NOT NULL PRIMARY KEY
) ENGINE = MEMORY
AS
SELECT custom_id
FROM custom_item_master
WHERE kind BETWEEN 10 AND 19
;

CREATE TEMPORARY TABLE removed_board_set_ids (
    set_id INT UNSIGNED NOT NULL PRIMARY KEY
) ENGINE = MEMORY
AS
SELECT DISTINCT item_set.set_id
FROM custom_item_set AS item_set
JOIN removed_board_item_ids AS board_item
  ON board_item.custom_id = item_set.custom_id;

-- Remove catalog rows for individual board items and sets containing a board item.
DELETE shop
FROM custom_shop_master AS shop
LEFT JOIN removed_board_item_ids AS board_item
  ON board_item.custom_id = shop.custom_id
LEFT JOIN removed_board_set_ids AS board_set
  ON board_set.set_id = shop.custom_id
WHERE board_item.custom_id IS NOT NULL
   OR board_set.set_id IS NOT NULL;

-- Remove board ownership and set definitions that would otherwise reference removed items.
DELETE owned
FROM player_custom_item AS owned
JOIN removed_board_item_ids AS board_item
  ON board_item.custom_id = owned.custom_id;

DELETE item_set
FROM custom_item_set AS item_set
LEFT JOIN removed_board_item_ids AS board_item
  ON board_item.custom_id = item_set.custom_id
LEFT JOIN removed_board_set_ids AS board_set
  ON board_set.set_id = item_set.set_id
WHERE board_item.custom_id IS NOT NULL
   OR board_set.set_id IS NOT NULL;

DELETE item_master
FROM custom_item_master AS item_master
LEFT JOIN removed_board_item_ids AS board_item
  ON board_item.custom_id = item_master.custom_id
LEFT JOIN removed_board_set_ids AS board_set
  ON board_set.set_id = item_master.custom_id
WHERE board_item.custom_id IS NOT NULL
   OR board_set.set_id IS NOT NULL;

DROP TEMPORARY TABLE removed_board_set_ids;
DROP TEMPORARY TABLE removed_board_item_ids;

COMMIT;
