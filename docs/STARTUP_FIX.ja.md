# 起動失敗修正メモ（Phase 1+2）

最終更新: 2026-09-17

## 症状

exe をダブルクリックしても何も起きない（トレイにも出ない）。

## 原因（コードレビュー）

1. `Main` の Mutex 名が公式 / Store 版と共有（`_RUNCAT_MUTEX`）。既に別ビルドがトレイにいるとメッセージなしで終了。
2. `Main` 外周に `catch` がなく、初期化例外がコンソールなしで吞み込まれ「何も起きない」に見える。
3. `NetworkRepository` コンストラクタの `GetIPStatistics()` に例外処理がなく、VPN/仮想NIC 等でトレイ作成前に落ちる。

## このパッチで直すこと

- Mutex を `_RUNCAT365_PERSONAL_MUTEX` に分離。二重起動時は MessageBox。
- `Main` に起動ログ (`%LocalAppData%\RunCat365\startup.log`) と例外 MessageBox。
- `NetworkRepository` の NIC 統計取得を例外耐性化（プロセス全体を落とさない）。

## まだ後回し

- 設定 / `user.config` の再生成（Phase 3）
- `docs/HANDOFF.ja.md` の削除（引継ぎ完了後）
