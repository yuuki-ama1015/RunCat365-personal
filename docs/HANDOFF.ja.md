# RunCat365-personal 引き継ぎ指示書

最終更新: 2026-09-17  
リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal（private）  
配布: https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.0

この文書はチャット消失後も開発を再開できるようにするための引き継ぎメモです。詳細な検討リストは [BACKLOG.ja.md](./BACKLOG.ja.md) を参照。

---

## 1. プロジェクト概要

- 上流: [runcat-dev/RunCat365](https://github.com/runcat-dev/RunCat365)（Apache-2.0）の**個人用 private 派生**
- 技術: .NET 9 / WinForms トレイアプリ + 設定 UI は WebView2
- 目的: 自分用にトレイ指標・表示モード・素材まわりを拡張する（上流への PR 前提ではない）

---

## 2. 進め方（必須）

特定ソフトに限らず、このユーザーとの開発では次を守る。

1. **いきなり実装しない**  
   方針相談 → 決める単位を小さく確定 →（画面があれば）ワイヤー → Phase 分け → 実装
2. **GitHub 完結**  
   コード変更・PR・マージ・Release はリモート側だけ。ユーザー PC のローカル作業ツリーは触らない前提
3. **確認ポイント**  
   push / マージ前、大きな方針のあとは一度確認する
4. **文脈の永続化**  
   決まった方針・検討リスト・一区切りメモはチャットだけでなくリポジトリの `docs/` に残す
5. **セーフポイント**  
   使用量や中断時は commit / PR / Release をセーフポイントにする

新しいチャットで再開するときの短い指示例:

> GitHub 完結で、方針→Phase→PR の流れ。ローカルは触らない。決まったことは docs に残して。まずは方針から。リポは yuuki-ama1015/RunCat365-personal。やりたいことは ○○。引き継ぎは docs/HANDOFF.ja.md と docs/BACKLOG.ja.md を読んで。

---

## 3. いまの状態（2026-09 時点）

### main に入っている主なもの

| 領域 | 内容 |
|------|------|
| 設定 UI | WebView2、ホーム / 個別設定 / 素材、サイドバー完全収納、シード色 `#5f9ea0` |
| 表示モード | ランナー / 色の変化 / 静止画モード（静止画は単独専用） |
| 色の変化 | 半透明赤オーバーレイ、16 段、濃さ 0–100、ランナーと併用可 |
| 静止画 | 素材名「静止画モード用」、2–16 PNG、負荷で切替、なめらか切替（300ms・指標ごと ON/OFF） |
| プレビュー | 個別設定で負荷スライダーつき実画像。静止画編集は全コマサムネ |
| GIF | ランナー編集・静止画編集から GIF 取込、上限超過は均等間引き |
| 配布 | Actions `release-win-x64.yml`、タグ `personal-v*`、zip `RunCat365-personal-win-x64.zip` |

### ラベル・用語（確定）

- トレイメニュー開き口: **設定**
- 左メニュー: **ホーム** → **個別設定** → **素材**（**ランナー用** / **静止画モード用**）
- 3 モード: **ランナー** / **色の変化** / **静止画モード**
- クロスフェード UI 名: **なめらか切替**

### 見送り・検討中

→ [BACKLOG.ja.md](./BACKLOG.ja.md)

- 温度の閾値 UI
- 背景除去
- ミニレール（完全収納確定済み。やるなら方針変更）

### 注意

- 設定 UI には **WebView2 ランタイム**が必要（最近の Windows には入っていることが多い）
- Linux 上では `net*-windows` をビルドできない。配布 zip は GitHub Actions（windows-latest）で作る
- 個人 Release は Store / WAP 提出用ではない

---

## 4. 再開時の手順

1. このファイルと `BACKLOG.ja.md` を読む
2. 直近の Release / 開いている PR を確認する  
   - Releases: https://github.com/yuuki-ama1015/RunCat365-personal/releases  
   - Actions: `.github/workflows/release-win-x64.yml`
3. やりたいことをユーザーに確認し、**方針から**始める
4. 実装は PR → 確認 → マージ。一区切りならタグ `personal-v*` で zip を更新
5. 新しく見送った項目は `BACKLOG.ja.md` を更新する

### 新しい Release zip の出し方

- タグを push: `personal-v1.0.1` など（`personal-v*`）
- または Actions の `workflow_dispatch`
- 成果物: `RunCat365-personal-win-x64.zip`（win-x64 self-contained）

---

## 5. 参考リンク

- リポジトリ: https://github.com/yuuki-ama1015/RunCat365-personal
- バックログ: https://github.com/yuuki-ama1015/RunCat365-personal/blob/main/docs/BACKLOG.ja.md
- 現行 Release: https://github.com/yuuki-ama1015/RunCat365-personal/releases/tag/personal-v1.0.0
- 上流: https://github.com/runcat-dev/RunCat365
