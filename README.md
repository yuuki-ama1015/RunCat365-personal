# RunCat365-personal

Personal derivative of [runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365).

Upstream is a cute running cat animation on the Windows Taskbar (`C#` / Win32 / `.NET 9.0`).

## 日本語での説明

**RunCat365-personal** は、公式の [RunCat365](https://github.com/runcat-dev/RunCat365) をベースにした個人向け派生版です。Windows タスクバー上で動く猫アニメに、個人利用向けの機能を足しています。

### 主な機能（このフォーク）

- トレイインジケーター最大 4 つ（CPU / GPU / メモリ / 温度）
- GPU 使用率（使用分のみ）の表示
- ストレージ情報で固定ドライブをまとめて表示
- 温度→スピード連動
- トレイの **設定** から WebView2 設定ウィンドウ（ホーム / 個別設定 / 素材）
- 色の変化・静止画モード・GIF 取込 など

### ダウンロード

最新の個人ビルド（Windows x64・自己完結 zip）:

- [Release personal-v1.0.2](https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.2)
- 資産: `RunCat365-personal-win-x64.zip`

解凍して `RunCat 365.exe` を実行してください。設定 UI には [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) が必要です（最近の Windows では多くがプリインストール）。

Windows が温度を公開しない PC で CPU 温度を使うには、[PawnIO 2.0 以降](https://pawnio.eu/) の導入が必要です。初回導入は管理者承認が必要で、導入後に RunCat365 を再起動してください。[温度取得の詳細・確認手順](docs/TEMPERATURE_LHM.ja.md)。

### ビルド

- 要件: Windows 10 19041.0 以上、.NET 9 SDK / Visual Studio
- `RunCat365.sln` を開いてビルド、または `dotnet build RunCat365.sln`

### ライセンス・帰属

Apache License 2.0（[LICENSE](./LICENSE)）。著作権は upstream（Takuto Nakamura / Studio Kyome および貢献者）に帰属します。本リポジトリは公式 Store 版ではありません。

---

## Attribution & license

- **Upstream:** [https://github.com/runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365)
- **License:** Apache License 2.0 (see [LICENSE](./LICENSE)) — same as upstream
- This repository is a personal fork/derivative. It does **not** claim authorship of the upstream project (copyright remains with Takuto Nakamura / Studio Kyome and upstream contributors).

## Personal changes

Local derivative with up to four tray indicators (CPU / GPU / Memory / Temperature), GPU dedicated usage (used only), all ready fixed drives in storage info, and temperature→speed mapping. Tray **設定** opens the WebView2 settings window（ホーム / 個別設定 / 素材）. See commit history for details.

## Download

Latest personal Windows x64 self-contained zip:

- [personal-v1.0.2](https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.2) — `RunCat365-personal-win-x64.zip`

## Build / run

- Requirement: Windows 10 version 19041.0 or higher, .NET 9 SDK / Visual Studio
- Open `RunCat365.sln` and build, or: `dotnet build RunCat365.sln`

Official Microsoft Store listing (upstream): https://apps.microsoft.com/detail/9nw5lpnvwfwj
