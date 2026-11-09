# AGENTS.md — RunCat365-personal

旧 `CLAUDE.md` の内容をここに統合。Claude Code は `@AGENTS.md` を読む。

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## You Must

- Respond in the same language the user is writing in.
- Commit to Git only when instructed.
- If multiple requirements are given in a single instruction, divide the commits into appropriate sizes/granularities.
- When creating a pull request, use the `.claude/skills/create-pr` skill so the project's `.github/pull_request_template.md` is followed.

## Project Overview

Windows Forms application (.NET 9.0 / C#) distributed via the Microsoft Store. Runs as a system tray icon whose animation reflects system load.

**Top-level layout:**

- `RunCat365.sln` — Main solution
- `RunCat365/` — Main application project
- `WapForStore/` — Windows Application Packaging project for the Microsoft Store

**Build:** Open `RunCat365.sln` in Visual Studio. Supported platforms: x64, x86, ARM64. Target framework: .NET 9.0 (Windows 10.0.26100.0).

**Releasing — version numbers must be updated in two places:**

1. `RunCat365/RunCat365.csproj` — `<Version>X.Y.Z</Version>` (3-digit)
2. `WapForStore/Package.appxmanifest` — `Version="X.Y.Z.0"` in `<Identity>` (4-digit)

## Architecture (patterns, not file lists)

- **Entry point:** `Program.cs` hosts the tray application context that owns the timers, the tray icon, and the context menu.
- **Repository pattern for system metrics:** Each metric has a `*Repository` class that produces a matching `*Info` struct (CPU / GPU / memory / storage / network / temperature). Add a new metric by adding a paired repository and info struct.
- **Animation loop:** A 1-second fetch timer refreshes the `*Info` structs; a separate animate timer advances frames at a rate derived from the `SpeedSource` setting. Theme-aware icon recoloring lives in `BitmapExtension`.
- **Custom runners:** User-defined animations are loaded from `profiles.json` and managed by the `CustomRunner*` classes; the active profile is stored in the `CustomRunnerName` setting.
- **EndlessGame:** Mini-game living in `EndlessGameForm` and its collaborator classes.
- **Settings & resources:** Persisted user preferences in `Properties/UserSettings.settings`; embedded images/icons in `Properties/Resources.resx`.

## Localization

- Localized strings live in `Properties/Strings.resx` (English default) and sibling `Properties/Strings.{locale}.resx` files. Any new string must be added to every variant.
- Per-locale font and culture settings live in `SupportedLanguage.cs`.
- When adding a brand-new locale (not just new strings), follow the `.claude/skills/add-locale` skill.

## Coding Rules

General contribution rules — indentation style, `var` usage, single-change PRs, English-only code, license, and the rest — are documented in [`CONTRIBUTING.md`](CONTRIBUTING.md). Follow those first.

Project-specific additions:

- Do not write comments within the source code. Use names that make intent obvious.
- Write abbreviations such as URL or ID in either all lowercase or all uppercase (do not use Upper Camel Case for them).
- Do not use abbreviations like `img` for `image` or `cnt` for `count`.

## GitHubで開発を行う場合のルール
GitHubのリポジトリで開発を行う場合は、以下に従うこと。

作業を始める前に、リポジトリのルートにある PROGRESS.md を読むこと。なければルートに作成すること。
作業の最後に、PROGRESS.md を以下の構成で更新し、コミット・プッシュすること。

# 進捗と方針
## 今後の実装方針
（決まった設計・技術選定・やり方。変わったら書き換える）
## 完了したこと
（日付と内容を1行ずつ追記する）
## 次にやること
（優先順に並べる。終わったら「完了したこと」へ移す）
## 未解決の問題・メモ
（バグ、迷っている点、人間に確認したいこと）
