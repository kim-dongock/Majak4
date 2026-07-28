"""Convert specific legacy /common/images/*.him files to PNG with blue→transparent."""
from pathlib import Path
from PIL import Image

SRC = Path(r"C:\hange_archive\typing\legacy\common\images")
DST = Path(r"C:\hange_archive\typing\public\assets\images")
TRANS = (0, 0, 255)
NAMES = [
    "_ChannelInfoBg.him", "_ChannelInfoBg2.him",
    "_MainframeBorderBottom.him", "_MainFrameCornerBottom.him",
    "_MiniChnl_ViewRoomLstBtn.him",
    "_MyInfo_LevelGuageBar.him", "_MyInfo_LevelGuageBg.him", "_MyInfo_LevelGuageSide.him",
    "_VScrollArrow.him", "_VScrollPage.him",
    "_VScrollThumb_Member.him", "_VScrollThumb_Room.him",
]

ok = fail = 0
for n in NAMES:
    s = SRC / n
    if not s.exists():
        print(f"MISS {n}"); fail += 1; continue
    try:
        img = Image.open(s).convert("RGBA")
        data = [(0, 0, 0, 0) if (r, g, b) == TRANS else (r, g, b, a) for (r, g, b, a) in img.getdata()]
        img.putdata(data)
        outname = n.replace(".him", ".png")
        img.save(DST / outname)
        print(f"OK   {outname} {img.size}")
        ok += 1
    except Exception as e:
        print(f"ERR  {n}: {e}"); fail += 1
print(f"== {ok} ok, {fail} fail")
