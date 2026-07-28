#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
音楽ファイルのエンコーディング自動感知・変換スクリプト
UTF-8 と CP932 の両方に対応
"""

import os
from pathlib import Path

def convert_file_encoding(file_path):
    """
    ファイルのエンコーディングを自動感知して UTF-8 に統一
    """
    try:
        content = None
        
        # UTF-8で読み込み試す (復旧ファイルはUTF-8)
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
        except UnicodeDecodeError:
            # UTF-8失敗時はCP932で試す
            try:
                with open(file_path, 'r', encoding='cp932') as f:
                    content = f.read()
            except UnicodeDecodeError:
                return False
        
        # UTF-8で書き込み (既にUTF-8なら変わらない)
        with open(file_path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(content)
        
        return True
    except Exception as e:
        return False


def main():
    """メイン処理"""
    print("=== 音楽ファイル エンコーディング自動感知・変換 ===\n")
    
    music_dir = Path(r'c:\hange_archive\typing\public\assets\music')
    
    if not music_dir.exists():
        print(f"[ERROR] ディレクトリが見つかりません: {music_dir}")
        return 1
    
    success_count = 0
    error_count = 0
    
    for i in range(1, 292):
        music_id = f"{i:03d}"
        txt_file = music_dir / f"music{music_id}" / f"music{music_id}.txt"
        
        if txt_file.exists():
            if convert_file_encoding(str(txt_file)):
                print(f"[OK] music{music_id}.txt")
                success_count += 1
            else:
                print(f"[SKIP] music{music_id}.txt")
    
    print(f"\n=== 完了 ===")
    print(f"処理完了: {success_count} ファイル")
    
    return 0


if __name__ == '__main__':
    import sys
    sys.exit(main())
