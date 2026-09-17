# 起動失敗修正メモ（Phase 1+2+3）

最終更新: 2026-09-17

## 症状

exe をダブルクリックしても何も起きない（トレイにも出ない）。

## 原因

1. `Main` の Mutex 名が公式 / Store 版と共有（`_RUNCAT_MUTEX`）。既に別ビルドがトレイにいるとメッセージなしで終了。
2. `Main` 外周に `catch` がなく、初期化例外がコンソールなしで吞み込まれ「何も起きない」に見える。
3. `NetworkRepository` の NIC 統計取得に例外処理がなく、VPN/仮想NIC 等でトレイ作成前に落ちる。
4. （Phase 3）`UserSettings.IndicatorsMigrated` に `[UserScopedSetting]` / `[DefaultSettingValue]` が無く、既存ユーザー設定読み込みで `SettingsPropertyNotFoundException` により起動失敗。

## このパッチで直すこと

- Mutex を `_RUNCAT365_PERSONAL_MUTEX` に分離。二重起動時は MessageBox。
- `Main` に起動ログ (`%LocalAppData%\RunCat365\startup.log`) と短い例外 MessageBox（詳細はログ）。
- `NetworkRepository` の NIC 統計取得を例外耐性化。
- `IndicatorsMigrated` を正式な UserScoped 設定として登録し、欠落時はレガシー移行へフォールバック。

## まだ後回し

- `docs/HANDOFF.ja.md` の削除（引継ぎ完了後）
