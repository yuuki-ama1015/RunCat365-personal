# 進捗と方針

## 今後の実装方針
- 上流 RunCat365（Apache-2.0）の個人派生。トレイ指標・温度連動・WebView2設定などを個人用途で拡張する。
- 方針相談 → 小さなPhase → PR → 確認 → マージ。リリースは `personal-v*` タグまたは `release-win-x64.yml` 手動実行。
- 上流へPRしない。このリポジトリの default branch のみにコミットする。
- 詳細ルールは AGENTS.md。温度の技術詳細は `docs/TEMPERATURE_LHM.ja.md`。

## 完了したこと
- 最大4トレイ指標、GPU／ストレージ表示、温度→スピード、WebView2設定、色変化・静止画・GIF取込など
- 温度取得の案内・フィルター改善と personal-v1.0.7 反映
- 2026-10-18 コードレビュー高／中／低優先の修正PRをマージ
- 2026-11-09 PROGRESS.md運用を導入
- 2026-11-09 旧 `docs/HANDOFF.ja.md` / `docs/BACKLOG.ja.md` / `docs/REVIEW_2026-10.ja.md` を統合削除（履歴 `9c0e230` 等）

## 次にやること
1. 見送り候補の再判断: 温度閾値UI（内部おおよそ40℃→アイドル／95℃→最大）、背景除去、設定ファイル全面Designer再生成
2. レビュー対応後の回帰確認（起動、温度取得、設定WebView）
3. 必要なら次タグ（例 personal-v1.0.8）で配布ZIPとハッシュ確認

## 未解決の問題・メモ
- 温度取得は環境依存。PawnIO 等の導入と、場合により管理者実行が必要（詳細は TEMPERATURE_LHM 文書）
- 設定UIには WebView2 Runtime が必要。個人Releaseは Store / WAP 提出用ではない
- ビルド: `dotnet build RunCat365.sln`。Windows x64 ZIP は GitHub Actions で生成
- 公開リポジトリのため、個人用ローカルパスや端末固有情報を新規に書かない
- オープンIssueなし
