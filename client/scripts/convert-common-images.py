#!/usr/bin/env python3
"""
legacy/common/images/*.him → public/assets/images/common/*.png

.him files are standard BMP. Pure blue (0,0,255) is used as the
transparency colorkey (same convention as typing/Images).
"""

from pathlib import Path
from PIL import Image

REPO   = Path(__file__).resolve().parent.parent
SRC    = REPO / "legacy/common/images"
DST    = REPO / "public/assets/images/common"
SRC_EX = REPO / "legacy/common/ex/images"
DST_EX = REPO / "public/assets/images/common/ex"

TRANS = (0, 0, 255)


def convert_one(src: Path, dst: Path) -> bool:
    try:
        img = Image.open(src).convert("RGBA")
        px = img.load()
        w, h = img.size
        for y in range(h):
            for x in range(w):
                r, g, b, _ = px[x, y]
                if (r, g, b) == TRANS:
                    px[x, y] = (0, 0, 0, 0)
        dst.parent.mkdir(parents=True, exist_ok=True)
        img.save(dst, "PNG")
        return True
    except Exception as e:
        print(f"  ERR {src.name}: {e}")
        return False


def run(src_dir: Path, dst_dir: Path) -> None:
    if not src_dir.is_dir():
        print(f"skip (missing): {src_dir}")
        return
    ok = fail = 0
    for src in sorted(src_dir.iterdir()):
        if not src.is_file():
            continue
        suf = src.suffix.lower()
        if suf == ".him":
            dst = dst_dir / (src.stem + ".png")
            if convert_one(src, dst):
                ok += 1
            else:
                fail += 1
        elif suf in (".bmp", ".png", ".gif"):
            # raw bitmap / static asset → copy as-is (preserve frames for .gif)
            dst_dir.mkdir(parents=True, exist_ok=True)
            dst = dst_dir / src.name
            try:
                dst.write_bytes(src.read_bytes())
                ok += 1
            except Exception as e:
                print(f"  ERR copy {src.name}: {e}")
                fail += 1
    print(f"{src_dir.name}: ok={ok} fail={fail}  →  {dst_dir}")


if __name__ == "__main__":
    run(SRC, DST)
    run(SRC_EX, DST_EX)
