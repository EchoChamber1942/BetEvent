# BetEvent

Configurable betting and event control plugin for Rust servers.  
Rustサーバー向けの設定可能なベット・イベント管理プラグインです。

---

## Overview / 概要

BetEvent is a configurable betting and event control plugin for Rust servers.

It is designed for tournaments, prediction games, community events, and other scrap-based betting activities. Players use a clear in-game betting UI, while server admins control the full event flow through a dedicated admin panel.

BetEvent は Rust サーバー向けのベット・イベント管理プラグインです。  
トーナメント、予想イベント、コミュニティイベントなど、スクラップを使った各種ベット運営を想定しています。  
プレイヤーは見やすいベットUIを利用し、管理者は専用の管理UIからイベント全体の進行を操作できます。

---

## Features / 主な機能

- Player betting UI  
- Admin control UI  
- Configurable option count  
- Custom labels for each option  
- Scheduled start and close support  
- Automatic close handling  
- Refund all bets  
- Event reset controls  
- Result finalization and payout processing  
- Pending reward recovery for offline players  
- English and Japanese language support  

- プレイヤー用ベットUI  
- 管理者用コントロールUI  
- 枠数の変更対応  
- 各枠ラベルのカスタム対応  
- 開始・締切の予約設定対応  
- 自動締切対応  
- 全額返金対応  
- イベントリセット対応  
- 結果確定と配当処理対応  
- オフラインプレイヤーへの保留報酬返却対応  
- 英語・日本語対応  

---

## Commands / コマンド

- `/bet`  
  Opens the player betting UI.  
  プレイヤー用ベットUIを開きます。

- `/beteventadmin`  
  Opens the admin UI for server admins.  
  サーバー管理者用の管理UIを開きます。

- `/bet result <option>`  
  Finalizes the result for server admins.  
  管理者が結果を確定します。

- `/beteventcfg options <count>`  
  Changes option count for server admins.  
  管理者が枠数を変更します。

- `/beteventcfg labels <label1,label2,...>`  
  Changes option labels for server admins.  
  管理者が各枠ラベルを変更します。

---

## Permissions / 権限

- No custom permissions  
- Admin features are restricted to Rust server admins  

- 専用permissionはありません  
- 管理機能は Rust サーバー管理者のみ利用できます  

---

## Typical Event Flow / 基本的な運用の流れ

1. Set the number of options  
2. Adjust labels if needed  
3. Open betting immediately or schedule start and close times  
4. Players place bets  
5. Close entries manually or let the timer close them automatically  
6. Finalize the winning option  
7. Payouts are processed automatically  

1. 枠数を設定  
2. 必要に応じてラベルを調整  
3. 即時開始または開始・締切を予約設定  
4. プレイヤーがベット  
5. 手動または自動で受付を締切  
6. 当選枠を確定  
7. 配当を自動処理  

---

## Screenshots / スクリーンショット

### Player UI
![Player UI](screenshots/Player%20betting%20interface.png)

### Admin UI
![Admin UI](screenshots/Admin%20control%20interface.png)

---

## Language Support / 言語対応

BetEvent includes English and Japanese language support.  
Language files are generated automatically when needed.

BetEvent は英語と日本語に対応しています。  
必要に応じて言語ファイルを自動生成します。

---

## Notes / 注意事項

- Option count changes should only be made when no bets remain  
- Offline payout recovery is supported for pending rewards  
- Designed for practical server event operation with clear UI and simple control flow  

- 枠数変更はベット残存がない状態で行ってください  
- オフライン報酬の保留返却に対応しています  
- 実運用しやすい、見やすさ重視のイベント管理フローを目指しています  

---

## uMod / 公開ページ

uMod page:  
https://umod.org/plugins/BRKkjA9y8Z

Example:  
`https://umod.org/plugins/...`
