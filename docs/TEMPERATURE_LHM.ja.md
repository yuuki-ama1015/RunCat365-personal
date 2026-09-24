# 温度取得と LibreHardwareMonitor（LHM）

最終更新: 2026-09-24

## 原因と対応

このアプリが使用する **LibreHardwareMonitorLib 0.9.6 は PawnIO を使用する**。
WinRing0 の有無では動作条件を判定できない。CPU センサー名が列挙されても、
ドライバー経由の読み取りができなければ値は null になる。

2026-09-24 に Core i5-10400F の実機で確認したこと:

- 導入前は常用アプリに同梱された LHM 自身の `PawnIo.IsInstalled` が false。
- 導入前に CPU を 3 回更新しても、Package / Core 温度はすべて null。
- 公式 LHM v0.9.6 は PawnIO 未導入時にインストールを案内するが、この派生アプリには案内がなかった。
- 同版に同梱された PawnIO 2.1.0 インストーラーの Authenticode は Valid（署名者 namazso）。

ユーザー承認後、PawnIO 2.1.0 を導入（終了コード 0、サービス Running）。
通常ユーザー権限では引き続き 5 回とも温度未取得だったが、管理者権限ではアプリと同じ
TemperatureRepository で **51 / 50 / 49 / 47 / 49 ℃** を 5 回連続取得できた。
原因は **PawnIO 未導入と読み取り権限不足**。この機種が非対応という従来の見込みを訂正する。

## 導入

1. [PawnIO 公式サイト](https://pawnio.eu/) から **2.0 以降**を導入する。Windows 全体へのドライバー追加なので初回は管理者承認が必要。
2. RunCat365 を終了して再起動する。LHM は PawnIO の導入状態をプロセス内で保持するため、設定画面の開き直しだけでは反映されない。
3. 温度未取得なら RunCat365 を終了し、`RunCat 365.exe` を右クリック → **管理者として実行**。この PC ではこの起動方法で取得確認済み。
4. 設定の温度インジケーターを有効にし、トレイの温度を確認する。
5. 未取得なら `%LocalAppData%\RunCat365\startup.log` の PawnIO バージョン・管理者権限・センサー値を確認する。

ドライバーが導入済みでも、権限・ドライバーの動作状態・機種によって読み取れない場合がある。
アプリはドライバーを自動インストールせず、通常起動時に自動昇格もしない。
既存の通常権限の自動起動でも、この PC の CPU 温度は取得できない。温度を使う場合は上記の起動方法を使う。

## 挙動

1. Windows `Thermal Zone Information` を優先（出典: **システム**）。
2. 有効な値が取れないとき LHM の CPU、Motherboard / SuperIO へフォールバック（既存の出典表示: **CPU**）。
3. 設定画面で PawnIO 不足・管理者実行の必要性・それ以外の未取得を区別して案内する。
4. `Distance to TjMax` は実温度ではなく上限温度までの差なので集計しない。NaN / Infinity も除外する。

## 検証

.NET 9 SDK のある Windows で実行する（テスト用パッケージは追加不要）:

```powershell
dotnet run --project tests/TemperatureChecks -c Release -p:Platform=x64
```

実機でアプリと同じ TemperatureRepository を使って 5 回取得する:

```powershell
dotnet run --project tests/TemperatureChecks -c Release -p:Platform=x64 -- --probe
```

`--probe` は温度未取得なら終了コード 1 を返す。通常の回帰テストは実センサーを必要としない。
Release ワークフローでも回帰テストを実行する。

設定画面の案内は `node tests/TemperatureChecks/settings-ui.cjs` で 4 分岐を確認する。

## 公式実装の根拠

- [LHM v0.9.6 PawnIO 導入確認と案内](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitor/UI/MainForm.cs)
- [LHM v0.9.6 PawnIO のドライバーアクセス](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/PawnIo/PawnIo.cs)
- [LHM v0.9.6 Intel MSR の読み取り](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/v0.9.6/LibreHardwareMonitorLib/PawnIo/IntelMsr.cs)
