---
applyTo: 'client/**,public/**,scripts/**'
description: 'レガシー画像リソースの変換・配置・スプライト規約'
---

# AP-06 Resource (画像・サウンド・スプライト)

## 1. ソース形式

レガシーの画像は `legacy/**/*.him` / `*.hso` / `*.hsm` 形式で保存されており、
内部実体は無圧縮 BMP もしくはそれを束ねたアーカイブ。`scripts/convert-resources.py`,
`scripts/inspect-hso.py` が一括変換を担当する。

変換後の PNG は `public/assets/images/` 以下に配置し、
**透過色キーは青 `(0, 0, 255)`** を `RGBA (0,0,0,0)` に置換する。

## 2. ボタンスプライトの 4 フレーム規約 ⚠️ 重要

Hangame レガシーのボタンスプライトは横方向に **4 フレーム** 連結された
1 枚の PNG。`width = frameWidth × 4`。フレーム順は以下で固定:

| フレーム # | x オフセット (47px 幅の場合) | 状態 |
| --- | --- | --- |
| **1 番目** | `0 0` | **normal** (通常) |
| **2 番目** | `-47px 0` | **disabled** (非活性) |
| **3 番目** | `-94px 0` | **hover** (マウスオーバー) |
| **4 番目** | `-141px 0` | **pressed** (マウス押下中) |

ユーザーの口頭表現「1 번째 / 2 번째 / 3 번째 / 4 번째」は
それぞれフレーム 0 / 1 / 2 / 3 を指す。

### CSS テンプレート

```css
.someButton {
  width: 47px; height: 27px;
  background-image: url('/assets/images/common/_XxxBtn.png');
  background-position: 0 0;                   /* 1 normal */
}
.someButton:hover    { background-position: -94px 0; }  /* 3 hover */
.someButton:active   { background-position: -141px 0; } /* 4 pressed */
.someButton:disabled { background-position: -47px 0; cursor: not-allowed; } /* 2 disabled */
/* 選択中トグルなど常時 pressed を見せたい場合は frame 4 を使う */
.someButtonActive    { background-position: -141px 0; }
```

> ❌ よくある間違い: hover を 2 フレーム目に当ててしまう。
> 2 フレーム目はグレーアウト用なので必ず disabled に割り当てる。

## 3. 命名規約

- レガシーのファイル名 (`_RefreshBtn.him` 等) は **そのまま** 維持し、
  拡張子のみ `.png` に変換する。スクリプトの再実行で上書きされても破壊されない。
- 配置先: `public/assets/images/<群>/<元ファイル名>.png`
  - 共通ボタン類: `public/assets/images/common/`
  - シーン専用: `public/assets/images/<scene>/`

## 4. 検証

PNG が破損していないか疑う前にまず MD5 でレガシー `.him` 内の BMP データと
比較する。一致していればクライアント側の表示ロジック (frame index, CSS) を疑う。
