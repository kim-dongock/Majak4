# AP-02 セキュリティ / 認証・認可

## 1. ユーザー認証フロー (Hangame ログインクッキー)

### 概要
本システムは NHN Hangame の既存認証基盤を利用する。
ゲームサーバーは独自のアカウント管理を行わず、ブラウザが保持する Hangame ログインクッキーを復号することでユーザーを識別する。

### クッキー仕様
- HTTP Cookie 名: `login`
- 値の形式: `hangame={URL_ENCODED_CSV}` (本番) / `hangametest={URL_ENCODED_CSV}` (alpha/local)
- CSV は 28 フィールド (HangameLoginCookieOrder 準拠)
- フィールド [0] = `userid`、[1] = `password`、[2] = `name` など一部は独自アルゴリズムで暗号化 (packString)
- `password` は平文ではない。CookieEncryptor/packString された値なので、必ず unpackString してからアプリ内の `password` として扱う。

### 復号アルゴリズム (unpackString)
実装クラス: `server/src/Services/HangameCookieDecryptor.cs`

1. prefix 除去 → URL デコード → カンマ CSV 分割
2. 暗号フィールドを unpackString で復号
   - 末尾 4 文字が CRC-16 CCITT 変形チェックサム (hex)。不一致は即 reject
   - 残り文字列をカスタム Base64 (64 文字 alphabet) でバイト列にデコード
   - XOR chain 復号 (先頭バイトがシード R)
   - Shift-JIS (MS932) でテキストにデコード
3. `userid` フィールドを `memberId` として使用

### Hangame password / HanCoin GSC 連携 (重要)

レガシーではゲーム起動時にユーザーがパスワードを入力しない。ハンコイン照会に使う password は、Hangame 起動情報から渡される内部パスワード値である。

レガシー根拠:
- `client/legacy/BASE/include/ProtocolKey.h`: `keyPwd = "k111e"`
- `client/legacy/BASE/common/GlobalInfo.cpp`: `PropagateGameStrInfo()` が `GI_GAMESTR` の `keyPwd` を `GI_MYINFO` にコピーする
- `client/legacy/client/HgComM/HgMainFrame.cpp`: `psGameStr.GetValue(G::keyPwd, ...)` を `m_Member.m_szPwd` にコピーする
- `client/legacy/client/HgMajak2/ItemShopDlg.cpp`: `m_pMember->m_szPwd` を `CGSHanCoin::InitHanCoin()` に渡す
- `client/legacy/client/HgMajak2/libXtHancoin/xtHanCoin_Inquiry_Sub.cpp`: GSC に `wdp=xtEncodeToWeb::Pack(password)` を送る
- `server/legacy/java/jp/hangame/ssl/bill/factory/InquiryModelFactory.java`: GSC は `wdp` を `BillUtils.unpackString()` する
- `server/legacy/java/jp/hangame/ssl/bill/checker/NamePasswordCheckerImpl.java`: unpack 後の password を `MEUSERMT.PASSWORD` と文字列比較する (`-103` は不一致)

移植ルール:
- Hangame login cookie の `password` フィールドは packString 済みとして unpackString する。48 文字程度の packed 値をそのまま GSC に送ってはいけない。
- レガシー gamestring に `k111e` が存在する場合は、それを HanCoin 用 password として優先する。
- `k111e` がない Web 起動では、login cookie の unpack 済み `password` を HanCoin 用 password として使う。
- HanCoin GSC 送信時は `mid`, `wdp`, `tim` をすべて `xtEncodeToWeb::Pack` 互換で pack する。`chk` は unpack 済みの `memberId@password@timestamp@a4$+sWx7` から MD5 を作る。
- ログには password 値そのものを出力しない。source (`k111e`, `cookie.password` など) と length のみを出す。

### 認証エンドポイント
`POST /auth/majak-login`
- リクエスト: JSON body `{ loginCookie }` または HTTP `Cookie: login=...` ヘッダー
- 処理: 復号 → `typing_rat` レコードの取得または初回作成 → プレイヤー情報返却
- 失敗時: 401 (復号失敗・クッキー未設定) を返す。エラー詳細は外部に露出しない

### 開発環境用テスト認証
`POST /auth/test-login?userId={id}` — 開発環境専用。本番では利用不可。

---

## 2. 楽曲ファイルの保護 (署名付き URL)

### 概要
楽曲 OGG ファイルは認証済みユーザーにのみ配信する。
直接 URL アクセスを防ぐため、HMAC-SHA256 署名付きの一回限り有効なトークンを使用する。

### 配信フロー
1. クライアントが WebSocket で `tpgc2e` (MusicDLStart) コマンドを送信
2. サーバーが `MusicTokenService.GenerateToken(musicIndex, memberId)` で署名トークンを発行
3. クライアントはトークン付き URL `GET /api/music/{index}/stream?token=...` でファイルを取得

### トークン仕様
実装クラス: `server/src/Services/MusicTokenService.cs`

- 形式: `{payload}.{signature}`
  - payload = Base64Url(`{musicIndex}:{memberId}:{expiryUnixSeconds}`)
  - signature = Base64Url(HMAC-SHA256(payload, SecretKey))
- 有効期限: デフォルト 10 分 (`MusicToken:LifetimeMinutes` で設定)
- **一回限り使用**: Redis (NX EX) または ConcurrentDictionary でフラグ管理。再利用は 401

### ファイル配信時のセキュリティ設定
- `Cache-Control: no-store` / `Pragma: no-cache` — ブラウザのディスクキャッシュへの保存を禁止
- Range リクエスト対応 (HTML5 `<audio>` の seek に必要)
- ファイルパスはユーザー入力を直接使用せず、musicIndex から内部でパスを組み立てる (パストラバーサル対策)

### 設定キー
```
MusicToken:SecretKey       — HMAC 署名キー (必須。環境変数等で管理)
MusicToken:LifetimeMinutes — トークン有効期限 (デフォルト: 10)
```
