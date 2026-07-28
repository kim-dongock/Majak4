#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from pathlib import Path
from Crypto.Cipher import Blowfish
import struct

KEY1 = bytes([
    0x5c,0x4d,0x83,0xbf,0x37,0x87,0xf6,0xa8,0xc4,0xda,0xdb,0xfd,0x2e,0x47,0x57,0xfa,
    0xf9,0xab,0x25,0x3b,0x20,0x8d,0xe5,0x8f,0x8a,0xe0,0x14,0x82,0xca,0x72,0xb2,0xb3,
    0xf8,0x75,0x3b,0x65,0xd6,0xf4,0xa4,0xed
])
KEY2 = bytes([
    0xd9,0xef,0x99,0x30,0x73,0xb3,0x5c,0x9a,0x57,0xf0,0x89,0x2a,0x1c,0x2b,0xe2,0x38,
    0xd5,0xd7,0x67,0x76,0x04,0xdf,0x4a,0xd3,0xab,0x57,0x4c,0x64,0xaa,0x95,0x89,0x15,
    0x8d,0x1f,0x52,0x08,0x2a,0x07,0xf3,0x6f
])
IV = b'\x00' * 8
ROOT = Path(r'c:\hange_archive\typing')
LEGACY_DIR = ROOT / 'legacy' / 'typing' / 'Resource' / 'ClientMusicData' / 'typingmusics' / 'typing' / 'Musics'
PUBLIC_DIR = ROOT / 'public' / 'assets' / 'music'
TARGETS = [17, 83]


def derive_key() -> bytes:
    cipher = Blowfish.new(KEY1, Blowfish.MODE_OFB, IV)
    return cipher.decrypt(KEY2).rstrip(b'\x00')


def decrypt_hso(path: Path, key: bytes) -> bytes:
    data = path.read_bytes()
    pad = (8 - len(data) % 8) % 8
    if pad:
        data += b'\x00' * pad
    cipher = Blowfish.new(key, Blowfish.MODE_OFB, IV)
    return cipher.decrypt(data)


def parse_hso(blob: bytes):
    magic, number, size = struct.unpack_from('<III', blob, 0)
    if magic != 0x314B4150:
        raise ValueError(f'bad magic: {magic:08X}')
    offset = 48
    files = []
    for _ in range(number):
        raw = blob[offset:offset + 40]
        name = raw[:32].split(b'\x00', 1)[0].decode('ascii', errors='replace')
        off, siz = struct.unpack_from('<II', raw, 32)
        files.append((name, off, siz))
        offset += 40
    return files


def extract_txt(path: Path) -> str:
    key = derive_key()
    blob = decrypt_hso(path, key)
    files = parse_hso(blob)
    txt_entry = next((entry for entry in files if entry[0].lower().endswith('.txt')), None)
    if not txt_entry:
        raise ValueError('txt entry not found')
    _, off, siz = txt_entry
    raw = blob[off:off + siz].rstrip(b'\x00')
    try:
        return raw.decode('cp932')
    except UnicodeDecodeError:
        return raw.decode('cp932', errors='ignore')


def main():
    key = derive_key()
    print('derived key ready', key[:8].hex())
    for music_id in TARGETS:
        hso_path = LEGACY_DIR / f'music{music_id:03d}.hso'
        out_dir = PUBLIC_DIR / f'music{music_id:03d}'
        out_file = out_dir / f'music{music_id:03d}.txt'
        if not hso_path.exists():
            print(f'[SKIP] missing {hso_path}')
            continue
        txt = extract_txt(hso_path)
        out_dir.mkdir(parents=True, exist_ok=True)
        out_file.write_text(txt, encoding='utf-8', newline='\n')
        print(f'[OK] music{music_id:03d}.txt -> {len(txt)} chars')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
