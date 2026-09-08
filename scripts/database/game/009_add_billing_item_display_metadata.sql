-- 既存 majak_game DB 用: 便利アイテムの商品表示情報
ALTER TABLE billing_item_master
    ADD COLUMN image_url VARCHAR(255) NOT NULL DEFAULT '' AFTER item_description,
    ADD COLUMN sort_order INT UNSIGNED NOT NULL DEFAULT 0 AFTER image_url;

UPDATE billing_item_master
SET item_description = CASE sub_code
    WHEN 'MJ2001' THEN '交流広場及び段位戦の場代が無料になります。ハイ卓は対象外です。使用中に対局を終了すると残り回数が1回減ります。'
    WHEN 'MJ2002' THEN '交流広場及び段位戦の場代が無料になります。ハイ卓は対象外です。使用中に対局を終了すると残り回数が1回減ります。'
    WHEN 'MJ2003' THEN '交流広場及び段位戦の場代が無料になります。ハイ卓は対象外です。使用中に対局を終了すると残り回数が1回減ります。'
    WHEN 'MJ2004' THEN '交流広場及び段位戦の場代が無料になります。ハイ卓は対象外です。使用中に対局を終了すると残り回数が1回減ります。'
    WHEN 'MJ2101' THEN '1日の間、獲得できる龍珠が2倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2102' THEN '3日の間、獲得できる龍珠が2倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2103' THEN '7日の間、獲得できる龍珠が2倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2104' THEN '30日の間、獲得できる龍珠が2倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2201' THEN '1日の間、獲得できる龍珠が3倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2202' THEN '3日の間、獲得できる龍珠が3倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2203' THEN '7日の間、獲得できる龍珠が3倍になります。対局終了時に使用中である必要があります。'
    WHEN 'MJ2204' THEN '30日の間、獲得できる龍珠が3倍になります。対局終了時に使用中である必要があります。'
END,
image_url = CASE sub_code
    WHEN 'MJ2001' THEN '/assets/images/game/items/mj_shop_item_sell_coin_01.png'
    WHEN 'MJ2002' THEN '/assets/images/game/items/mj_shop_item_sell_coin_01.png'
    WHEN 'MJ2003' THEN '/assets/images/game/items/mj_shop_item_sell_coin_02.png'
    WHEN 'MJ2004' THEN '/assets/images/game/items/mj_shop_item_sell_coin_03.png'
    WHEN 'MJ2101' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_01.png'
    WHEN 'MJ2102' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_03.png'
    WHEN 'MJ2103' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_05.png'
    WHEN 'MJ2104' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_07.png'
    WHEN 'MJ2201' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_02.png'
    WHEN 'MJ2202' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_04.png'
    WHEN 'MJ2203' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_06.png'
    WHEN 'MJ2204' THEN '/assets/images/game/items/mj_shop_item_sell_ryu_08.png'
    ELSE image_url
END,
sort_order = CASE sub_code
    WHEN 'MJ2001' THEN 10 WHEN 'MJ2002' THEN 20 WHEN 'MJ2003' THEN 30 WHEN 'MJ2004' THEN 40
    WHEN 'MJ2101' THEN 50 WHEN 'MJ2102' THEN 60 WHEN 'MJ2103' THEN 70 WHEN 'MJ2104' THEN 80
    WHEN 'MJ2201' THEN 90 WHEN 'MJ2202' THEN 100 WHEN 'MJ2203' THEN 110 WHEN 'MJ2204' THEN 120
    ELSE sort_order
END
WHERE item_code IN ('MJ20', 'MJ21', 'MJ22');