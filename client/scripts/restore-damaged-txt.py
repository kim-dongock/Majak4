#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
レガシーHSOファイルからテキストを再抽出して修復するスクリプト
Blowfish OFB復号化 + TXT抽出 + UTF-8変換
"""

import os
import sys
from pathlib import Path
from Crypto.Cipher import Blowfish

# Blowfish暗号化キー（レガシーから取得）
BLOWFISH_KEY = "知彼知己者百戰不殆不知彼而知己一勝一負不".encode('shift-jis')
IV = b'\x00' * 8

def decrypt_hso(hso_path):
    """
    HSO ファイルを復号化
    
    Args:
        hso_path: HSO ファイルのパス
    
    Returns:
        tuple: (ogg_data, hsl_data, txt_data) またはエラー時 None
    """
    try:
        with open(hso_path, 'rb') as f:
            encrypted_data = f.read()
        
        # Blowfish OFB モード
        cipher = Blowfish.new(BLOWFISH_KEY, Blowfish.MODE_OFB, IV)
        decrypted_data = cipher.decrypt(encrypted_data)
        
        return decrypted_data
    except Exception as e:
        print(f"[ERROR] 復号化失敗 {hso_path}: {e}")
        return None


def extract_txt_from_decrypted(decrypted_data, music_id):
    """
    復号化されたHSOデータからTXTを抽出
    
    HSO構造（推測）:
    ├─ OGG ヘッダ "OggS"
    ├─ HSL ヘッダ (または別のマーク)
    └─ TXT テキスト
    
    Args:
        decrypted_data: 復号化されたデータ
        music_id: 音楽ID（ログ用）
    
    Returns:
        str: 抽出されたTXTテキスト または None
    """
    try:
        # OggS マーク位置を探す
        ogg_start = decrypted_data.find(b'OggS')
        if ogg_start == -1:
            print(f"[WARN] music{music_id:03d}: OGG マークが見つかりません")
            return None
        
        # OGGデータの終わり位置を推測
        # 通常、OGGファイル終了後にHSLやTXTがある
        # ここでは簡略化して最後部分を探す
        
        # "," で始まるテキスト部分を探す（TXT構造）
        # または複数行の日本語テキストを探す
        
        # より確実な方法：最後のセクションを取得
        # HSO内部では通常 OGG -> HSL -> TXT の順序
        
        # 簡略版：データから可読テキスト部分を抽出
        text_start = decrypted_data.rfind(b'\x00')  # 最後のnullバイト
        if text_start != -1:
            potential_txt = decrypted_data[text_start+1:]
            
            # 有効なテキストか確認（日本語またはASCII）
            try:
                decoded = potential_txt.decode('utf-8', errors='ignore')
                if ',' in decoded or 'あ' in decoded or 'か' in decoded:
                    return decoded.strip()
            except:
                pass
        
        # 別の方法：CP932で試す
        try:
            potential_txt = decrypted_data[-2000:]  # 最後の2000バイト
            decoded = potential_txt.decode('cp932', errors='ignore')
            if ',' in decoded:
                # カンマで始まる行から取得
                lines = decoded.split('\n')
                result = []
                for line in lines:
                    if line.strip() and (',' in line or len(line.strip()) > 2):
                        result.append(line)
                
                if result:
                    return '\n'.join(result).strip()
        except:
            pass
        
        return None
    
    except Exception as e:
        print(f"[ERROR] TXT抽出失敗 music{music_id:03d}: {e}")
        return None


def restore_damaged_files():
    """
    損傷したファイルを復元
    """
    print("=== HSO ファイルから TXT を復抽出 ===\n")
    
    # 損傷ファイル一覧
    damaged_ids = [12, 17, 26, 38, 45, 61, 83, 108, 111, 112, 191]
    
    legacy_hso_dir = Path(r'c:\hange_archive\typing\legacy\typing\Resource\ClientMusicData\typingmusics\typing\Musics')
    output_dir = Path(r'c:\hange_archive\typing\public\assets\music')
    
    success_count = 0
    error_count = 0
    
    for music_id in damaged_ids:
        hso_file = legacy_hso_dir / f'music{music_id:03d}.hso'
        output_file = output_dir / f'music{music_id:03d}' / f'music{music_id:03d}.txt'
        
        if not hso_file.exists():
            print(f"[SKIP] music{music_id:03d}: HSO ファイルが見つかりません")
            continue
        
        print(f"[処理中] music{music_id:03d}...", end=' ')
        
        # 復号化
        decrypted = decrypt_hso(str(hso_file))
        if decrypted is None:
            print("[FAIL]")
            error_count += 1
            continue
        
        # TXT抽出
        txt_content = extract_txt_from_decrypted(decrypted, music_id)
        if txt_content is None:
            print("[FAIL - TXT抽出失敗]")
            error_count += 1
            continue
        
        # UTF-8で保存
        try:
            output_dir_path = output_file.parent
            output_dir_path.mkdir(parents=True, exist_ok=True)
            
            with open(output_file, 'w', encoding='utf-8', newline='\n') as f:
                f.write(txt_content)
            
            print(f"[OK] ({len(txt_content)} 文字)")
            success_count += 1
        except Exception as e:
            print(f"[FAIL - 保存失敗] {e}")
            error_count += 1
    
    print(f"\n=== 完了 ===")
    print(f"復元成功: {success_count} ファイル")
    print(f"復元失敗: {error_count} ファイル")
    
    return 0 if error_count == 0 else 1


if __name__ == '__main__':
    sys.exit(restore_damaged_files())
