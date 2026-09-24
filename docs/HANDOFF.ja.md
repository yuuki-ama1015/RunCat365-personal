# RunCat365-personal 引き継ぎ指示書

最終更新: 2026-09-24
リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal（**public**）  
現行配布: https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.6

この文書はチャット消失後も開発を再開できるようにするための引き継ぎメモです。詳細な検討リストは [BACKLOG.ja.md](./BACKLOG.ja.md) を参照。

---

## 1. プロジェクト概要

- 上流: [runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365)（Apache-2.0）の**個人用派生**（公開リポ）
- 技術: .NET 9 / WinForms トレイアプリ + 設定 UI は WebView2
- 目的: 自分用にトレイ指標・表示モード・素材まわりを拡張する（上流への PR 前提ではない）
- ユーザー実機の常用フォルダ: `C:\Users\yuuki\Downloads\RunCat365`（入れ替え時は `RunCat365-backup-before-vNNN` に退避）

---

## 2. 進め方（必須）

特定ソフトに限らず、このユーザーとの開発では次を守る。

1. **いきなり実装しない**  
   方針相談 → 決める単位を小さく確定 →（画面があれば）ワイヤー → Phase 分け → 実装
2. **GitHub 完結**  
   コード変更・PR・マージ・Release はリモート側だけ。ユーザー PC のローカル作業ツリーは触らない前提（配布 zip の Downloads 入れ替えは別）
3. **確認ポイント**  
   push / マージ前、大きな方針のあとは一度確認する
4. **文脈の永続化**  
   決まった方針・検討リスト・一区切りメモはチャットだけでなくリポジトリの `docs/` に残す
5. **セーフポイント**  
   使用量や中断時は commit / PR / Release をセーフポイントにする

新しいチャットで再開するときの短い指示例:

> GitHub 完結で、方針→Phase→PR の流れ。ローカル作業ツリーは触らない。決まったことは docs に残して。まずは方針から。リポは yuuki-ama1015/RunCat365-personal。やりたいことは ○○。引き継ぎは docs/HANDOFF.ja.md と docs/BACKLOG.ja.md を読んで。

---

## 3. いまの状態（2026-09-23）

### 現行タグ

**personal-v1.0.6**（2026-09-23）。PR #25 マージ済み。開いている PR なし。

### main に入っている主なもの（v1.0.0 以降の追加含む）

| 領域 | 内容 | 目安タグ |
|------|------|----------|
| 設定 UI | WebView2、ホーム / 個別設定 / 素材、サイドバー完全収納、シード色 `#5f9ea0` | v1.0.0〜 |
| 表示モード | ランナー / 色の変化 / 静止画モード（静止画は単独専用） | v1.0.0〜 |
| 色の変化 | 半透明赤オーバーレイ、16 段、濃さ 0–100、ランナーと併用可 | v1.0.0〜 |
| 静止画 | 素材名「静止画モード用」、2–16 枚、負荷で切替、なめらか切替 | v1.0.0〜 |
| GIF | ランナー・静止画とも PNG/GIF 取込可（静止画編集の文言も PNG/GIF） | v1.0.6 で案内明確化 |
| 素材案内 | 設定 UI の素材ページに「使える素材」ガイド（枚数・サイズ・用途） | **v1.0.6 / PR #25** |
| 起動安定化 | Mutex 分離、startup.log、例外 MessageBox、Network 例外耐性、IndicatorsMigrated 属性 | v1.0.1〜v1.0.2 |
| 温度 | Thermal Zone 優先 → LHM CPU / Motherboard・SuperIO フォールバック、出典ラベル、二重 Update | v1.0.3〜v1.0.5 |
| トレイ | メニュー外クリックで閉じる（`SetForegroundWindow` + `AutoClose`） | v1.0.5 |
| 公開 | リポ public + README 日本語 | PR #20 |
| 配布 | Actions `release-win-x64.yml`、タグ `personal-v*`、zip `RunCat365-personal-win-x64.zip` | — |

詳細メモ:

- 起動系: [STARTUP_FIX.ja.md](./STARTUP_FIX.ja.md)
- 温度 / LHM: [TEMPERATURE_LHM.ja.md](./TEMPERATURE_LHM.ja.md)

### ラベル・用語（確定）

- トレイメニュー開き口: **設定**
- 左メニュー: **ホーム** → **個別設定** → **素材**（**ランナー用** / **静止画モード用**）
- 3 モード: **ランナー** / **色の変化** / **静止画モード**
- クロスフェード UI 名: **なめらか切替**
- 温度出典表示例: `温度: 72°C（CPU）`＝LHM、`温度: 48°C（システム）`＝Thermal Zone。ボード由来も UI 上は **CPU** にまとめる

### 素材の実制限（コード準拠・v1.0.6 で UI 案内済み）

- **ランナー**: PNG（透過推奨）または GIF。2〜30 コマ。トレイ向けに高さ約 32px へ自動リサイズ。アニメ用
- **静止画**: PNG または GIF。2〜16 コマ。左＝低負荷 → 右＝高負荷。約 32×32 へ自動リサイズ

### 温度まわり（2026-09-24 調査再開）

2026-09-24 のユーザー依頼「このアプリ開発を引き継いで。まず温度に関する問題を解決」で調査・修正を再開。

この PC（Intel Core i5-10400F）での実測:

- Thermal Zone Information / WMI 温度は空
- LHM はセンサ**名**は見えるが値はすべて null（二重 Update 後も同じ）
- **原因となる前提不足を確認: LHM 0.9.6 が使う PawnIO が未導入（LHM 自身の IsInstalled=false）**
- 以前の WinRing0 調査は現行依存に対応していなかった。この機種を非対応とはまだ判定できない
- 不足ドライバーの案内・診断ログ、TjMax 差分と非有限値の除外、回帰テストを修正ブランチに追加
- ドライバー導入は Windows 全体を変更するためユーザー承認待ち。導入後の実測は未完了

ログ: `%LocalAppData%\RunCat365\startup.log`

次の確認: PawnIO 導入後に `tests/TemperatureChecks -- --probe` で実温度が取得できるか確認する。
作業はこのタスク専用の新規クローンで実施。常用 Downloads は未変更。GitHub 公開・Release 更新は未実施。

### 見送り・検討中

→ [BACKLOG.ja.md](./BACKLOG.ja.md)

- 温度の閾値 UI
- 背景除去
- ミニレール（完全収納確定済み。やるなら方針変更）
- Phase3 設定ファイル再生成（IndicatorsMigrated 属性対応済み。全面再生成は後回し）

### 注意

- 設定 UI には **WebView2 ランタイム**が必要（最近の Windows には入っていることが多い）
- Linux 上では `net*-windows` をビルドできない。配布 zip は GitHub Actions（windows-latest）で作る
- 個人 Release は Store / WAP 提出用ではない
- Cursor Cloud Agents が使えない場合は `gh` / GitHub Contents API + PR で進めてきた

---

## 4. 再開時の手順

1. このファイルと `BACKLOG.ja.md` を読む
2. 直近の Release / 開いている PR を確認する  
   - Releases: https://github.com/yuuki-ama1015/RunCat365-personal/releases  
   - Actions: `.github/workflows/release-win-x64.yml`
3. やりたいことをユーザーに確認し、**方針から**始める
4. 実装は PR → 確認 → マージ。一区切りならタグ `personal-v*` で zip を更新
5. ユーザーが Downloads 入れ替えを求めたら、zip を展開して `C:\Users\yuuki\Downloads\RunCat365` を差し替え（実行中なら先に終了、旧フォルダは `RunCat365-backup-before-vNNN`）
6. 新しく見送った項目は `BACKLOG.ja.md` を更新する

### 新しい Release zip の出し方

- タグを push: `personal-v1.0.7` など（`personal-v*`）
- または Actions の `workflow_dispatch`
- 成果物: `RunCat365-personal-win-x64.zip`（win-x64 self-contained）

---

## 5. 直近のリリース履歴（要約）

| タグ | 内容 |
|------|------|
| personal-v1.0.6 | 素材ページの使える画像/GIF 案内、静止画「PNGのみ」文言修正（PR #25） |
| personal-v1.0.5 | トレイ外クリック閉じ、LHM 二重 Update |
| personal-v1.0.4 | LHM Motherboard/SuperIO + startup.log 強化 |
| personal-v1.0.3 | LHM CPU 温度フォールバック + 出典表示 |
| personal-v1.0.2 | 起動安定化一式 |
| personal-v1.0.1 | IndicatorsMigrated 起動クラッシュ修正 |
| personal-v1.0.0 | 個人機能の初期セーフポイント |

---

## 6. 参考リンク

- リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal
- バックログ: https://github.com/yuuki-ama1015/RunCat365-personal/blob/main/docs/BACKLOG.ja.md
- 現行 Release: https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.6
- 上流: https://github.com/runcat-dev/RunCat365
