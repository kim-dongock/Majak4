---
applyTo: "server/**,server.tests/**,client/src/api/**,client/src/store/**,client/src/screens/auth/**,admin/**"
description: "Google認証、会員登録、ゲームJWT、Refresh Cookie、member_noとpix、SignalR認可、MP課金セキュリティを確認・変更するときに参照する"
---

# AP-02 セキュリティ / 認証・認可

## 0. 基本原則

- 現行Web/モバイル版の基本認証はGoogle ID Tokenである。
- サーバーは `player_account` を管理し、Googleの `sub` を内部会員ID `member_no` に対応付ける。
- `POST /auth/majak-login` と `POST /auth/majak-register` は旧hange起動互換のために残す。新規機能の標準認証経路にしてはならない。
- RESTとSignalRの本人判定はゲームJWTから得た `member_no` を正本とする。
- クライアントが送信した `memberNo`、`memberId`、`pix`だけを根拠にDB更新や権限判定をしてはならない。
- パスワード、Google ID Token、Access Token、Refresh Token、決済事業者の秘密情報をログへ出力しない。

## 1. Googleログインと会員登録

### 1-1. ログイン

- `POST /auth/google-login` はGoogle ID Tokenを検証し、`google_sub` で `player_account` を検索する。
- `POST /auth/google-login-redirect` はGoogle GIS redirect mode用である。既存会員にはRefresh Cookieを発行してクライアントへ戻し、未登録なら登録フローへ遷移させる。
- 未登録の場合は `requiresRegistration=true` を返し、ID Tokenを5分間のHttpOnly Cookie `mj_pending_google_id_token` に一時保存できる。
- 既存会員では最終ログインと日次ミッションを更新し、`pix`、ゲームAccess Token、ローテーション済みRefresh Cookieを発行する。
- Google ID Tokenの `aud` は設定されたGoogle Client IDと必ず照合する。

### 1-2. 会員登録

- `POST /auth/google-register` は検証済みGoogle `sub`、ニックネーム、性別、出生年、アバターを使って登録する。
- ニックネームはtrim後4〜16文字で重複不可、性別は `M` / `F`、出生年は1900年から現在年、アバターは性別ごとの `AvatarCatalog` で検証する。
- `GamePlayerRepository.RegisterGoogleAsync` が `player_account`、`player_wallet`、`player_profile` を同一登録フローで作成し、DBが新しい `member_no` を採番する。
- 登録処理は同じGoogle `sub` に対して冪等に扱い、既存アカウントがあれば新しい会員を重複作成しない。
- Web登録は規約同意済みとして作成するが、`account_status=0` の承認待ちは維持する。停止中 `account_status=2` を迂回させてはならない。
- `GET /auth/check-nickname` は補助確認であり、登録時にも必ずサーバー側で再検証する。

### 1-3. レガシーhange互換

- `POST /auth/majak-login` は `login` Cookieまたは `loginCookie` bodyの `hangame=` / `hangametest=` 値を `HangameCookieDecryptor` で検証する互換経路である。
- `POST /auth/majak-register` はレガシーCookieから得た会員情報で旧アカウントを登録する互換経路である。
- `k111e`、launch URL、referrer、Cookie由来のpasswordは旧便利アイテム連携の互換値に限る。Google認証、ゲームJWT、MP残高の本人証明に使用しない。
- 互換passwordは値をログへ出さず、取得元と長さだけを記録する。

## 2. Access TokenとRefresh Cookie

### 2-1. ゲームAccess Token

- `GameAuthTokenService` はHMAC-SHA256の短期JWTを発行する。
- Claimは `sub=member_no`、`member_no`、`pix`、`jti` を含む。
- 検証時は署名、issuer、audience、有効期限を確認し、clock skewは30秒とする。
- `GameAuth:JwtSecret` は環境秘密として管理し、未設定時に既定値へフォールバックしてはならない。
- RESTは `Authorization: Bearer <token>`、SignalRは接続時の `access_token` またはAuthorization headerで検証する。

### 2-2. Refresh Cookie

- `AuthRefreshSessionService` がRefresh Tokenを発行し、Cookie名はサービス定義を正本とする。
- CookieはHttpOnly、環境に応じたSecure、適切なSameSite、明示的な有効期限を設定する。
- サーバー側には生Tokenではなくハッシュとセッション情報をRedisへ保存する。
- `POST /auth/refresh` は現在Tokenを検証・失効してから、新しい `pix`、Access Token、Refresh Tokenを発行する。
- `POST /auth/logout` はRefresh Tokenを失効させ、Cookieを削除する。
- 無効・期限切れ・アカウント不在時はCookieを消し、認証済みレスポンスを返さない。

## 3. `member_no` と `pix`

### 3-1. `member_no`

- `player_account.member_no` はDBが採番する永続内部IDであり、アカウントと全ゲームデータの主キーである。
- JWTの `sub` / `member_no`、Repository検索、監査ログ、サーバー内部セッションにだけ使用する。
- 通常のクライアントレスポンス、画面、ロビー、ルーム、他プレイヤー向けペイロードへ内部 `member_no` を公開しない。

### 3-2. `pix`

- `pix` は `PlayerSessionService.IssuePix(memberNo)` が `pix` + 16バイトの暗号学的乱数16進表現として発行する公開セッションIDである。
- `pix` はプロセスメモリの `_memberNoToPix` / `_pixToMemberNo` にだけ保持し、DBへ永続化しない。
- 同一サーバープロセス内では既存対応を再利用できるが、再起動やセッション再発行を越える永続IDとして扱わない。
- 認証レスポンスは `pix` を返し、旧クライアント互換の `memberNo` フィールドにも内部IDではなく同じ `pix` を返す。
- ロビー、チャンネル、ルーム、対局、チャット等でクライアントが参照するプレイヤーIDは原則 `pix` とする。

### 3-3. 信頼境界

- 自分自身を変更するREST APIはrequest body/queryの会員IDを使わず、JWTの `auth.MemberNo` を使う。
- API互換上 `pix` / `memberNo` を受け取る場合も、JWTの `auth.Pix` または `auth.MemberNo`、`PlayerSessionService` の対応関係と一致することを確認する。不一致は403とする。
- SignalRコマンドは `CommandContext.AuthMemberNo` / `AuthPix` を使い、ペイロードのIDだけから本人を確定しない。
- `ResolveMemberNo` は既知の `pix` を内部IDへ解決する補助であり、未認証の任意文字列を本人証明に昇格させる関数ではない。
- 他ユーザーを対象にできる操作は、対象を `pix` で指定しても別途権限・所属・同一ルーム等を検証する。

## 4. アカウント状態と管理者認証

- `account_status`: `0=承認待ち`、`1=プレイ可能`、`2=停止中` とする。
- 利用規約同意とプレイ承認は別状態であり、同意だけで承認待ちを解除しない。
- `/api/admin/*` はゲームJWTとは別の管理者JWTとroleを使う。ゲームJWTを管理者権限として受け入れない。
- 管理者による残高調整、アカウント承認・停止、商品変更はoperatorと変更前後を監査ログへ残す。

## 5. MP・商品購入セキュリティ

- GP・MP・龍珠の区分と表示はAP-16を正本とする。
- MP商品購入はサーバーが商品マスター、販売状態、価格、所持状況、残高を再検証し、クライアントの価格・付与量を信頼しない。
- MP差引きとアイテム付与はゲームDBの同一トランザクションで処理する。
- MPは無償分を先に、有償分を後に消費し、合計・有償・無償の前後残高を取引ログへ残す。
- 管理者MP調整は管理者JWTと必要roleを検証し、正の付与は無償MPとして扱う。
- `cash_product_master` と `cash_charge_order` は将来の直接購入用である。外部PG・アプリストアの完了通知またはレシート検証なしに購入完了や有償MP付与を行わない。
- 決済通知は注文IDと事業者取引IDで冪等化し、署名検証、金額・商品・会員照合、取消・返金、監査ログを実装する。
- 現時点では外部決済によるMP直接購入は未実装である。旧ハンゲーム購入ページやGSCを現行の決済済み経路として記述・利用しない。

## 6. 秘密情報とログ

- JWT secret、Google Client Secret、DB資格情報、Redis資格情報、OAuth Token、決済署名鍵はソースやクライアントbundleへ含めない。
- 認証失敗レスポンスは内部例外、Token内容、アカウント存在の不要な詳細を返さない。
- ログへは内部 `member_no` を監査目的で記録できるが、Token、Cookie、password、決済raw secretを記録しない。
- クライアントへ返すアカウント情報は必要最小限とし、出生年は本人の設定画面以外で他プレイヤーへ公開しない。
