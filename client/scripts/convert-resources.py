#!/usr/bin/env python3
"""
Legacy Hangame Majak2 Resource Converter
-----------------------------------------
Converts legacy Majak2 client resource files to web-compatible formats for
Phaser + React + TypeScript redev.

Dependencies:
  pip install Pillow pycryptodome

Source layout (legacy/client/Products/Resources/):
  majak3/
    Images/          main game images (.him)
    Images1-3/       skin variant images (.him)
    Sounds/          BGM + SE (.hso → OGG or WAV)
    Sounds1-3/       skin variant sounds
  images/            common lobby UI images (.him)
  mj1p/
    Images/          mj1p game images (.him)
    sounds/          mj1p sounds (.hso)
  skins/             skin bitmaps (.bmp)

Output structure (public/assets/):
  images/
    majak3/          majak3 Images* → .png  (preserves subdir)
    common/          images/ → .png
    mj1p/            mj1p/Images/ → .png
  sounds/            all .hso → .ogg (OGG) or .wav (RIFF/WAV)
  config/            .cfg → .json
"""

import sys
import shutil
import struct
from pathlib import Path

# ─── Paths ────────────────────────────────────────────────────────────────────
REPO_ROOT   = Path(__file__).resolve().parent.parent
JP_ROOT     = Path(r"C:\Hange\JAPANESE")
OUTPUT_ROOT = REPO_ROOT / "public/assets"

# Source directories
SRC_LEGACY_GAME      = JP_ROOT / "majak3"
SRC_COMMON          = JP_ROOT / "images"
# Output directories
OUT_IMAGES_GAME     = OUTPUT_ROOT / "images/game"
OUT_IMAGES_COMMON   = OUTPUT_ROOT / "images/common"
OUT_SOUNDS          = OUTPUT_ROOT / "sounds"

# Transparent color key used in all .him files (pure blue → alpha 0)
TRANS_COLOR = (0, 0, 255)

# Legacy draws these resources with regular Draw() / CMJBmpButton dialog
# controls, so blue title/button pixels must remain opaque.
OPAQUE_HIM_STEMS = {
    "_ShopReceiptMain1",
    "_ShopReceiptMain2",
    "_ShopReceiptMain2b",
    "_ShopReceiptMainCustom",
    "_ShopReceiptExitBtn",
    "_ShopReceiptBuyBtn",
    "_ShopReceiptYesBtn",
    "_ShopReceiptNoBtn",
    "_ShopReceiptOkBtn",
}

# .him files that are NOT images — skip conversion
# mj_images*.him : MD5 checksum database for skin integrity verification (MJGraph.cpp)
# Collection*.him : PKM archive of GIF images — unpack separately
SKIP_HIM_STEMS = {
    "mj_images", "mj_images_100001",
    "Collection", "Collection_r",
}


def ensure_dirs() -> None:
    for d in [OUT_IMAGES_GAME, OUT_IMAGES_COMMON, OUT_SOUNDS]:
        d.mkdir(parents=True, exist_ok=True)


# ─── .him → .png ──────────────────────────────────────────────────────────────
def convert_him_to_png(src: Path, dst: Path) -> bool:
    """
    .him files are standard BMP images.
    Pure blue (0, 0, 255) is used as the transparency color key.
    """
    try:
        from PIL import Image
    except ImportError:
        print("  ERROR: Pillow not installed. Run: pip install Pillow")
        sys.exit(1)

    try:
        dst.parent.mkdir(parents=True, exist_ok=True)
        img = Image.open(src).convert("RGBA")
        if src.stem not in OPAQUE_HIM_STEMS:
            pixels = img.load()
            w, h = img.size
            for y in range(h):
                for x in range(w):
                    r, g, b, a = pixels[x, y]
                    if r == TRANS_COLOR[0] and g == TRANS_COLOR[1] and b == TRANS_COLOR[2]:
                        pixels[x, y] = (0, 0, 0, 0)
        img.save(dst, "PNG")
        return True
    except Exception as e:
        print(f"  ERROR converting {src.name}: {e}")
        return False


def convert_him_dir(src_dir: Path, dst_dir: Path, recurse: bool = False) -> tuple[int, int]:
    """Convert all .him files in src_dir to PNG in dst_dir.
    If recurse=True, preserves subdirectory structure.
    Files in SKIP_HIM_STEMS are silently skipped (non-image bundles).
    Returns (ok, err) counts.
    """
    if not src_dir.is_dir():
        print(f"  SKIP (missing): {src_dir}")
        return 0, 0
    ok = err = 0
    pattern = "**/*.him" if recurse else "*.him"
    for src in sorted(src_dir.glob(pattern)):
        if src.stem in SKIP_HIM_STEMS:
            continue  # non-image file (checksum DB or PKM archive)
        rel = src.relative_to(src_dir) if recurse else Path(src.name)
        dst = dst_dir / rel.with_suffix(".png")
        if convert_him_to_png(src, dst):
            ok += 1
        else:
            err += 1
    return ok, err


# ─── .hso → .ogg / .wav ───────────────────────────────────────────────────────
def get_audio_magic(path: Path) -> bytes:
    try:
        return path.read_bytes()[:4]
    except Exception:
        return b""


def copy_hso(src: Path, dst_dir: Path) -> bool:
    """Copy .hso to dst_dir as .ogg (OggS), .wav (RIFF), or .mp3 (ID3).
    mjksounds.hso (bundled archives) are copied with .hso extension for later unpacking.
    Unknown format: copy with .hso extension for manual inspection.
    """
    magic = get_audio_magic(src)
    if magic == b"OggS":
        dst = dst_dir / (src.stem + ".ogg")
    elif magic == b"RIFF":
        dst = dst_dir / (src.stem + ".wav")
    elif magic[:3] == b"ID3":
        # ID3-tagged MP3 (BGM files)
        dst = dst_dir / (src.stem + ".mp3")
    elif src.stem == "mjksounds":
        # Bundled sound archive — keep as .hso; unpack separately with inspect-hso.py
        dst = dst_dir / src.name
        print(f"  SKIP (bundle) {src.name}: use inspect-hso.py to unpack")
        return True
    else:
        dst = dst_dir / src.name
        print(f"  WARN unknown format {src.name}: {magic.hex()}")
    try:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        return True
    except Exception as e:
        print(f"  ERROR copying {src.name}: {e}")
        return False


def copy_hso_dir(src_dir: Path, dst_dir: Path, recurse: bool = False) -> tuple[int, int]:
    """Copy all .hso files from src_dir to dst_dir.
    If recurse=True, preserves subdirectory structure under dst_dir.
    """
    if not src_dir.is_dir():
        return 0, 0
    ok = err = 0
    pattern = "**/*.hso" if recurse else "*.hso"
    for src in sorted(src_dir.glob(pattern)):
        if recurse:
            rel = src.relative_to(src_dir).parent
            out_dir = dst_dir / rel
            out_dir.mkdir(parents=True, exist_ok=True)
        else:
            out_dir = dst_dir
        if copy_hso(src, out_dir):
            ok += 1
        else:
            err += 1
    return ok, err


# ─── Main ─────────────────────────────────────────────────────────────────────
def main() -> None:
    ensure_dirs()
    total_ok = total_err = 0

    # ── [1] Legacy game Images* → public/assets/images/game/ ───────────────
    # Images/  : main game images (sprites, UI, effects, items, events)
    # Images1-3: skin variant images (pai design, board backgrounds)
    # Subdirectory structure is preserved under images/game/
    print("\n[1/3] Game images (.him → .png, subdirs preserved)")
    for subdir in ["Images", "Images1", "Images2", "Images3"]:
        src = SRC_LEGACY_GAME / subdir
        dst = OUT_IMAGES_GAME if subdir == "Images" else OUT_IMAGES_GAME / subdir
        ok, err = convert_him_dir(src, dst, recurse=True)
        print(f"      {subdir}: {ok} ok, {err} err")
        total_ok += ok; total_err += err
    print(f"      subtotal: {total_ok} ok, {total_err} err")
    total_ok = total_err = 0

    # ── [2] images/ (common lobby UI) → public/assets/images/common/ ──────
    print("\n[2/3] Common UI images (.him → .png)")
    ok, err = convert_him_dir(SRC_COMMON, OUT_IMAGES_COMMON, recurse=False)
    # .bmp files: copy as-is (no transparency key needed)
    for src in sorted(SRC_COMMON.glob("*.bmp")):
        try:
            shutil.copy2(src, OUT_IMAGES_COMMON / src.name)
            ok += 1
        except Exception as e:
            print(f"  ERROR copy {src.name}: {e}"); err += 1
    print(f"      {ok} ok, {err} err")
    total_ok = total_err = 0

    # ── [3] Legacy game Sounds* → public/assets/sounds/game/ ──────────────
    # .hso = OGG (BGM) → .ogg / RIFF → .wav / ID3 → .mp3
    # Sounds*: 재귀 (skin/ 서브디렉터리 포함)
    print("\n[3/3] Sounds (.hso → .ogg / .wav / .mp3)")
    for subdir in ["Sounds", "Sounds1", "Sounds2", "Sounds3"]:
        src_dir = SRC_LEGACY_GAME / subdir
        dst_dir = OUT_SOUNDS / "game" if subdir == "Sounds" else OUT_SOUNDS / "game" / subdir
        ok, err = copy_hso_dir(src_dir, dst_dir, recurse=True)
        if ok or err:
            print(f"      game/{subdir}: {ok} ok, {err} err")
        total_ok += ok; total_err += err
    print(f"      subtotal: {total_ok} ok, {total_err} err")
    total_ok = total_err = 0

    print(f"\nDone -- assets written to: {OUTPUT_ROOT.resolve()}")


if __name__ == "__main__":
    main()
