-- 009 適用済み majak_game DB 用: 麻雀くじの商品画像と表示順
UPDATE billing_item_master
SET image_url = CASE sub_code
    WHEN 'MJ100' THEN '/assets/images/game/items/lot_item_01.png'
    WHEN 'MJ101' THEN '/assets/images/game/items/lot_item_02.png'
    WHEN 'MJ102' THEN '/assets/images/game/items/lot_item_03.png'
    WHEN 'MJ103' THEN '/assets/images/game/items/lot_item_01.png'
    WHEN 'MJ104' THEN '/assets/images/game/items/lot_item_02.png'
    WHEN 'MJ205' THEN '/assets/images/game/items/lot_item_03.png'
    ELSE image_url
END,
sort_order = CASE sub_code
    WHEN 'MJ100' THEN 10 WHEN 'MJ101' THEN 20 WHEN 'MJ102' THEN 30
    WHEN 'MJ103' THEN 40 WHEN 'MJ104' THEN 50 WHEN 'MJ205' THEN 60
    WHEN 'MJ2001' THEN 100 WHEN 'MJ2002' THEN 110 WHEN 'MJ2003' THEN 120 WHEN 'MJ2004' THEN 130
    WHEN 'MJ2101' THEN 140 WHEN 'MJ2102' THEN 150 WHEN 'MJ2103' THEN 160 WHEN 'MJ2104' THEN 170
    WHEN 'MJ2201' THEN 180 WHEN 'MJ2202' THEN 190 WHEN 'MJ2203' THEN 200 WHEN 'MJ2204' THEN 210
    ELSE sort_order
END
WHERE item_code IN ('MJ100', 'MJ101', 'MJ102', 'MJ103', 'MJ104', 'MJ105', 'MJ20', 'MJ21', 'MJ22');