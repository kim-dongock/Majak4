#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from pathlib import Path
from Crypto.Cipher import Blowfish
import struct
import sys

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


def decrypt(path: Path) -> bytes:
    data = path.read_bytes()
    pad = (8 - len(data) % 8) % 8
    if pad:
        data += b'\x00' * pad
    cipher = Blowfish.new(KEY, Blowfish.MODE_OFB, IV)
    return cipher.decrypt(data)


def derive_hso_key() -> bytes:
    seed = bytearray(KEY2)
    cipher = Blowfish.new(KEY1, Blowfish.MODE_OFB, IV)
    return cipher.decrypt(bytes(seed))


def parse_hso(blob: bytes):
    magic, number, size = struct.unpack_from('<III', blob, 0)
    if magic != 0x314B4150:
        raise ValueError(f'bad magic: {magic:08X}')
    offset = 48
    files = []
    for _ in range(number):
        raw = blob[offset:offset+40]
        name = raw[:32].split(b'\x00', 1)[0].decode('ascii', errors='replace')
        off, siz = struct.unpack_from('<II', raw, 32)
        files.append((name, off, siz))
        offset += 40
    return size, files


def try_decode(data: bytes):
    encodings = ['utf-8', 'utf-8-sig', 'cp932', 'shift_jis', 'euc_jp', 'utf-16le', 'utf-16']
    for enc in encodings:
        try:
            txt = data.decode(enc)
            printable = sum(1 for ch in txt if ch.isprintable() or ch in '\r\n\t')
            score = printable / max(1, len(txt))
            print(f'  decode {enc}: ok len={len(txt)} printable_ratio={score:.3f}')
            print('  preview:', repr(txt[:300]))
        except Exception as e:
            print(f'  decode {enc}: fail {e}')

    for enc in ['cp932', 'shift_jis']:
        txt = data.decode(enc, errors='ignore')
        printable = sum(1 for ch in txt if ch.isprintable() or ch in '\r\n\t')
        score = printable / max(1, len(txt))
        print(f'  decode {enc} ignore: len={len(txt)} printable_ratio={score:.3f}')
        print('  preview:', repr(txt[:300]))


def main(argv):
    key = derive_hso_key()
    print('derived key bytes:', key[:40].hex())
    print('derived key text :', repr(key.rstrip(b'\x00')))
    for arg in argv[1:]:
        path = Path(arg)
        print(f'== {path.name} ==')
        cipher = Blowfish.new(key.rstrip(b'\x00'), Blowfish.MODE_OFB, IV)
        data = path.read_bytes()
        pad = (8 - len(data) % 8) % 8
        if pad:
            data += b'\x00' * pad
        blob = cipher.decrypt(data)
        size, files = parse_hso(blob)
        print('size', size, 'files', files)
        for name, off, siz in files:
            if name.lower().endswith('.txt'):
                raw = blob[off:off+siz]
                print('txt raw first 64 bytes:', raw[:64].hex())
                try_decode(raw.rstrip(b'\x00'))

if __name__ == '__main__':
    sys.exit(main(sys.argv))
