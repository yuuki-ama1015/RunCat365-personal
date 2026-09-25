# RunCat365-personal 引き継ぎ指示書

最終更新: 2026-09-25
リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal（public）
現行配布: [personal-v1.0.7](https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.7)
直近の温度修正: [PR #26](https://github.com/yuuki-ama1015/RunCat365-personal/pull/26)（マージ済み）

この文書は、チャット履歴がなくても開発を再開できるように、現状・確認済みの前提・再開手順をまとめたものです。検討中の項目は [BACKLOG.ja.md](./BACKLOG.ja.md) を参照してください。

---

## 1. プロジェクト概要

- 上流: [runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365)（Apache-2.0）の個人用派生版
- 技術: .NET 9 / WinForms トレイアプリ。設定画面は WebView2
- 主な個人機能: CPU / GPU / メモリ / 温度のトレイ表示、温度連動、色の変化、静止画モード、PNG/GIF 素材取込
- ユーザー実機の常用フォルダー: `C:\Users\yuuki\Downloads\RunCat365`
- 現在の常用フォルダーは personal-v1.0.7 で更新済み。入れ替え時に作成した `RunCat365-backup-*` は削除済み

## 2. 開発方針

1. いきなり実装せず、まず現状と原因を確認する
2. 方針 → 小さな Phase → PR → 確認 → マージの順で進める
3. ソース変更は GitHub を中心に管理し、決まったことは `docs/` に残す
4. リリースは `personal-v*` タグまたは `release-win-x64.yml` の手動実行で作る
5. Windows のドライバー導入、管理者実行、既存アプリの入れ替えは対象と影響を確認してから行う

新しいチャットでの再開例:

> GitHub 完結で、方針 → Phase → PR の流れで進めてください。まず `docs/HANDOFF.ja.md` と `docs/BACKLOG.ja.md` を読み、現状を確認してから方針を相談してください。リポジトリは `yuuki-ama1015/RunCat365-personal` です。

## 3. 現在のリリース状態

- `personal-v1.0.7`: 温度取得の診断・案内・値の検証を含む現行リリース
- PR #26: マージ済み
- GitHub Actions: `.github/workflows/release-win-x64.yml` が Windows x64 self-contained ZIP を生成
- 配布資産: `RunCat365-personal-win-x64.zip`
- 対応コミット:
  - `797a997` — PawnIO 不足の診断、温度以外・無効値の除外
  - `21ccbab` — 管理者起動案内、実測値の記録

## 4. 温度問題の解決状況

### 原因と前提

- 使用ライブラリは `LibreHardwareMonitorLib 0.9.6`
- センサー名が列挙できても、値が `null` の場合は温度取得成功とは判断しない
- この環境では LHM が使う PawnIO が未導入だった
- PawnIO 2.1.0 を導入しても通常権限では取得できず、管理者権限での起動が必要だった

### 実機で確認済みの結果

対象 CPU は Intel Core i5-10400F。

- Thermal Zone / WMI: 有効な温度なし
- 通常権限の同一プローブ: 温度取得不可
- 管理者権限の同一プローブ: 47〜51℃を 5 回連続取得

したがって、この PC で温度を使うときは RunCat 365 を終了し、`RunCat 365.exe` を右クリックして「管理者として実行」する。通常の自動起動は昇格しない。

### 実装済みの防御

- Thermal Zone → LHM CPU → Motherboard / SuperIO の順でフォールバック
- 温度出典を UI に表示（CPU / システム）
- `Distance to TjMax` は温度値として扱わない
- `NaN`、`Infinity`、-50℃未満、150℃超を集計から除外
- PawnIO 不足、管理者権限不足、その他の取得失敗を区別して案内
- 起動診断を `%LocalAppData%\RunCat365\startup.log` に記録

詳細手順: [TEMPERATURE_LHM.ja.md](./TEMPERATURE_LHM.ja.md)

## 5. 開発・検証

### ビルド

```powershell
dotnet build RunCat365.sln
```

Windows x64 の配布 ZIP は Linux 上で作らず、GitHub Actions で生成する。

### 温度関連の回帰確認

```powershell
dotnet run --project tests/TemperatureChecks -c Release -p:Platform=x64
node tests/TemperatureChecks/settings-ui.cjs
```

実機プローブを行う場合は次を追加する。

```powershell
dotnet run --project tests/TemperatureChecks -c Release -p:Platform=x64 -- --probe
```

テストの期待値は、センサー名の列挙ではなく、有限かつ妥当な温度値だけが集計されること。ドライバー導入後は通常権限と管理者権限を同じ手順で比較する。

## 6. 再開手順

1. `docs/HANDOFF.ja.md` と `docs/BACKLOG.ja.md` を読む
2. `main`、最新タグ、開いている PR、Actions の状態を確認する
3. 目的と方針を決め、必要なら Phase に分ける
4. 変更をブランチで実装し、回帰確認を行う
5. PR を作成して確認後にマージする
6. 配布が必要なら `personal-v*` タグを作り、ZIP と SHA-256 を確認する
7. 完了した方針・保留項目・検証結果を `docs/` に更新する

### Release 手順

- タグ例: `personal-v1.0.8`
- 対象ワークフロー: `.github/workflows/release-win-x64.yml`
- トリガー: `personal-v*` タグ push または `workflow_dispatch`
- 成果物: `RunCat365-personal-win-x64.zip`
- リリース後は Actions の成功、ZIP の内容、起動、GitHub 上の資産ハッシュを確認する

## 7. 保留項目

- 温度の閾値 UI
- 背景除去
- ミニレール（サイドバー完全収納から方針を変える場合のみ）
- 設定ファイル全面再生成（`IndicatorsMigrated` 対応済みのため後回し）

## 8. 注意事項

- 設定 UI には WebView2 Runtime が必要
- 温度取得には環境によって PawnIO と管理者権限が必要
- 個人 Release は Microsoft Store / WAP 提出用ではない
- ローカルの実行中アプリを入れ替える場合は先に終了する
- `C:\Users\yuuki\Downloads\RunCat365` は常用フォルダーなので、作業用クローンや一時ファイルを置かない

## 9. 参考リンク

- リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal
- 現行 Release: https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.7
- Actions: https://github.com/yuuki-ama1015/RunCat365-personal/actions/workflows/release-win-x64.yml
- バックログ: [BACKLOG.ja.md](./BACKLOG.ja.md)
- 温度詳細: [TEMPERATURE_LHM.ja.md](./TEMPERATURE_LHM.ja.md)
- 上流: https://github.com/runcat-dev/RunCat365
