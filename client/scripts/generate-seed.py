#!/usr/bin/env python3
"""
generate-seed.py
レガシーOracleCSVデータをMySQL INSERT SQLファイルに変換します。

使用方法:
    python scripts/generate-seed.py

出力:
    scripts/seed/01_typing_item_change.sql
    scripts/seed/02_typing_item_mast.sql
    scripts/seed/03_typing_mission_mast.sql
    scripts/seed/04_typing_music_data.sql
    scripts/seed/05_typing_chanel_mast.sql
"""

import csv
import os
import re

# ─── パス設定 ──────────────────────────────────────────────────
REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSV_DIR   = os.path.join(REPO_ROOT, "legacy", "typing", "DB")
OUT_DIR   = os.path.join(REPO_ROOT, "scripts", "seed")

# CSV文字コード — レガシーOracleダンプはShift-JIS (CP932)
CSV_ENCODING = "cp932"

os.makedirs(OUT_DIR, exist_ok=True)


def esc(value: str) -> str:
    """MySQL文字列エスケープ (SQLインジェクション防止)."""
    if value is None or value == "":
        return "NULL"
    # バックスラッシュ先にエスケープ、次にシングルクォート
    value = value.replace("\\", "\\\\").replace("'", "\\'")
    return f"'{value}'"


def num(value: str, default: str = "0") -> str:
    """数値またはNULL返却."""
    v = value.strip() if value else ""
    if v == "" or v.upper() == "NULL":
        return "NULL"
    # 整数/実数チェック
    try:
        int(v)
        return v
    except ValueError:
        try:
            float(v)
            return v
        except ValueError:
            return default


def date_val(value: str) -> str:
    """日付文字列 → MySQL DATETIME リテラルまたはNULL."""
    v = value.strip() if value else ""
    if not v or v.upper() == "NULL":
        return "NULL"
    # Oracle: YYYY/MM/DD [HH:MM:SS] → MySQL: 'YYYY-MM-DD HH:MM:SS'
    v = v.replace("/", "-")
    if len(v) == 10:
        v += " 00:00:00"
    return f"'{v}'"


def read_csv(filename: str):
    path = os.path.join(CSV_DIR, filename)
    rows = []
    with open(path, encoding=CSV_ENCODING, errors="replace", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            rows.append(row)
    return rows


def write_sql(filename: str, header_comment: str, sql_lines: list[str]):
    path = os.path.join(OUT_DIR, filename)
    with open(path, "w", encoding="utf-8") as f:
        f.write(f"-- {header_comment}\n")
        f.write("-- scripts/generate-seed.py により自動生成\n")
        f.write("-- 手動編集不可\n\n")
        f.write("USE typing;\n\n")
        f.write(f"TRUNCATE TABLE {filename.split('_', 1)[1].rsplit('.', 1)[0]};\n\n")
        for line in sql_lines:
            f.write(line + "\n")
    print(f"[OK] {path}  ({len(sql_lines)} 行)")


# ─────────────────────────────────────────────────────────────────
# 1. TYPINGITEMCHANGET → typing_item_change
# ─────────────────────────────────────────────────────────────────
def gen_item_change():
    rows = read_csv("TYPINGITEMCHANGET.csv")
    lines = []
    for r in rows:
        line = (
            "INSERT INTO typing_item_change "
            "(item_id, change_item_id1, change_item_id2, change_item_id3, change_item_id4) VALUES "
            f"({num(r['ITEMID'])}, {num(r['CHANGEITEMID1'])}, {num(r['CHANGEITEMID2'])}, "
            f"{num(r['CHANGEITEMID3'])}, {num(r['CHANGEITEMID4'])});"
        )
        lines.append(line)
    write_sql("01_typing_item_change.sql", "アイテム交換マスター (TYPINGITEMCHANGET)", lines)


# ─────────────────────────────────────────────────────────────────
# 2. TYPINGITEMMAST → typing_item_mast
# ─────────────────────────────────────────────────────────────────
def gen_item_mast():
    rows = read_csv("TYPINGITEMMAST.csv")
    lines = []
    cols = (
        "game_id, item_id, item_name, item_kind, item_type, "
        "use_number_limit, get_number_limit, effect_time, price, "
        "premium_item, priority_rank, appearance, av_code, premium_price, "
        "t_music, effect, ope_type, operation, effect_type, shop_order_flag, t_point"
    )
    for r in rows:
        shop = num(r.get("SHOPORDERFLAG", ""), "NULL")
        line = (
            f"INSERT INTO typing_item_mast ({cols}) VALUES ("
            f"{esc(r['GAMEID'])}, {num(r['ITEMID'])}, {esc(r['ITEMNAME'])}, "
            f"{num(r['ITEMKIND'])}, {num(r['ITEMTYPE'])}, "
            f"{num(r['USENUMBERLIMIT'])}, {num(r['GETNUMBERLIMIT'])}, {num(r['EFFECTTIME'])}, "
            f"{num(r['PRICE'])}, {num(r['PREMIUMITEM'])}, {num(r['PRIORITYRANK'])}, "
            f"{num(r['APPEARANCE'])}, {esc(r.get('AVCODE', ''))}, {num(r['PREMIUMPRICE'])}, "
            f"{num(r['TMUSIC'])}, {num(r['EFFECT'])}, {num(r['OPETYPE'])}, "
            f"{num(r['OPERATION'])}, {num(r['EFFECTTYPE'])}, {shop}, {num(r['TPOINT'])}"
            ");"
        )
        lines.append(line)
    write_sql("02_typing_item_mast.sql", "アイテムマスター (TYPINGITEMMAST)", lines)


# ─────────────────────────────────────────────────────────────────
# 3. TYPINGMISSIONMAST → typing_mission_mast
# ─────────────────────────────────────────────────────────────────
def gen_mission_mast():
    rows = read_csv("TYPINGMISSIONMAST.csv")
    lines = []
    cols = (
        "mission_no, mission_level, mission_name, mission_info, "
        "music_index, player_cnt, type_level, score, combo_cnt, clear_cnt, "
        "item_no, join_money, must_exp, prize_flag, prize_val, prize_exp, prize_cnt, "
        "line_sort_flag, link_no, link_clear_cnt, "
        "prize_ava_name, prize_ava_cdm, prize_ava_cdf, valid_flg"
    )
    for r in rows:
        line = (
            f"INSERT INTO typing_mission_mast ({cols}) VALUES ("
            f"{num(r['MISSIONNO'])}, {num(r['MISSIONLEVEL'])}, "
            f"{esc(r.get('MISSIONNAME',''))}, {esc(r.get('MISSIONINFOCMT',''))}, "
            f"{num(r['MUSICINDEX'])}, {num(r['PLAYERCNT'])}, {num(r['TYPELEVEL'])}, "
            f"{num(r['SCORE'])}, {num(r['COMBOCNT'])}, {num(r['CLEARCNT'])}, "
            f"{num(r['ITEMNO'])}, {num(r['JOINMONEY'])}, {num(r['MUSTEXP'])}, "
            f"{num(r['PRIZEFLAG'])}, {num(r['PRIZEVAL'])}, {num(r['PRIZEEXP'])}, "
            f"{num(r['PRIZECNT'])}, {num(r['LINESORTFLAG'])}, "
            f"{esc(r.get('LINKNO',''))}, {esc(r.get('LINKCLEARCNT',''))}, "
            f"{esc(r.get('PRIZEAVANAME',''))}, {esc(r.get('PRIZEAVACDM',''))}, "
            f"{esc(r.get('PRIZEAVACDF',''))}, {num(r['VALIDFLG'],'1')}"
            ");"
        )
        lines.append(line)
    write_sql("03_typing_mission_mast.sql", "ミッションマスター (TYPINGMISSIONMAST)", lines)


# ─────────────────────────────────────────────────────────────────
# 4. TYPINGMUSICDATA → typing_music_data
# ─────────────────────────────────────────────────────────────────
def gen_music_data():
    rows = read_csv("TYPINGMUSICDATA.csv")
    lines = []
    cols = (
        "music_index, music_name, music_artist, music_write, music_composition, "
        "music_difficulty, music_appearance, music_select, demonstration_cnt, "
        "cost_t_point, sound_cost_t_point, score_correct_rate, sound_permission, "
        "evt_use_chk, img_type, remove_chk, start_dt, end_dt"
    )
    for r in rows:
        line = (
            f"INSERT INTO typing_music_data ({cols}) VALUES ("
            f"{num(r['MUSICINDEX'])}, {esc(r.get('MUSICNAME',''))}, "
            f"{esc(r.get('MUSICARTIST',''))}, {esc(r.get('MUSICWRITE',''))}, "
            f"{esc(r.get('MUSICCOMPOSITION',''))}, "
            f"{num(r['MUSICDIFFICULTY'])}, {num(r['MUSICAPPEARANCE'])}, "
            f"{num(r['MUSICSELECT'])}, {num(r['DEMONSTRATIONCNT'])}, "
            f"{num(r['COSTTPOINT'])}, {num(r['SOUNDCOSTTPOINT'])}, "
            f"{num(r['SCORECORRECTRATE'])}, {num(r['SOUNDPERMISSION'])}, "
            f"{esc(r.get('EVTUSECHK','N'))}, {num(r['IMGTYPE'])}, "
            f"{esc(r.get('REMOVECHK','N'))}, "
            f"{date_val(r.get('STARTDT',''))}, {date_val(r.get('ENDDT',''))}"
            ");"
        )
        lines.append(line)
    write_sql("04_typing_music_data.sql", "音楽マスター (TYPINGMUSICDATA)", lines)


# ─────────────────────────────────────────────────────────────────
# 5. CHANELMAST → typing_chanel_mast
# ─────────────────────────────────────────────────────────────────
def gen_chanel_mast():
    rows = read_csv("CHANELMAST.csv")
    lines = []
    cols = (
        "gubun, gameid, subid, chanelid, exedir, exefile, acsdir, acsfile, "
        "dbstring, dbusr, dbpwd, goservice, chanelport, chanelname, maxmember, "
        "maxroom, unitmoney, chaneltype, managetype, ghostdi, machine, maxnimember, "
        "sessionnum, srcount, srdirect, srindex, dbstring2, dbusr2, dbpwd2, "
        "dbstring3, dbusr3, dbpwd3, instancename"
    )
    for r in rows:
        line = (
            f"INSERT INTO typing_chanel_mast ({cols}) VALUES ("
            f"{esc(r.get('GUBUN', ''))}, {esc(r.get('GAMEID', ''))}, {esc(r.get('SUBID', ''))}, "
            f"{esc(r.get('CHANELID', ''))}, {esc(r.get('EXEDIR', ''))}, {esc(r.get('EXEFILE', ''))}, "
            f"{esc(r.get('ACSDIR', ''))}, {esc(r.get('ACSFILE', ''))}, {esc(r.get('DBSTRING', ''))}, "
            f"{esc(r.get('DBUSR', ''))}, {esc(r.get('DBPWD', ''))}, {esc(r.get('GOSERVICE', ''))}, "
            f"{esc(r.get('CHANELPORT', ''))}, {esc(r.get('CHANELNAME', ''))}, {esc(r.get('MAXMEMBER', ''))}, "
            f"{esc(r.get('MAXROOM', ''))}, {esc(r.get('UNITMONEY', ''))}, {esc(r.get('CHANELTYPE', ''))}, "
            f"{esc(r.get('MANAGETYPE', ''))}, {esc(r.get('GHOSTDI', ''))}, {esc(r.get('MACHINE', ''))}, "
            f"{esc(r.get('MAXNIMEMBER', ''))}, {esc(r.get('SESSIONNUM', ''))}, {num(r.get('SRCOUNT', ''))}, "
            f"{num(r.get('SRDIRECT', ''))}, {num(r.get('SRINDEX', ''))}, {esc(r.get('DBSTRING2', ''))}, "
            f"{esc(r.get('DBUSR2', ''))}, {esc(r.get('DBPWD2', ''))}, {esc(r.get('DBSTRING3', ''))}, "
            f"{esc(r.get('DBUSR3', ''))}, {esc(r.get('DBPWD3', ''))}, {esc(r.get('INSTANCENAME', ''))}"
            ");"
        )
        lines.append(line)
    write_sql("05_typing_chanel_mast.sql", "チャネルマスター (CHANELMAST)", lines)


if __name__ == "__main__":
    print("=== シードデータ生成開始 ===")
    gen_item_change()
    gen_item_mast()
    gen_mission_mast()
    gen_music_data()
    gen_chanel_mast()
    print("=== 完了 ===")
    print(f"出力パス: {OUT_DIR}")
