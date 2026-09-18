# 温度取得と LibreHardwareMonitor（LHM）

## 背景

Windows のパフォーマンスカウンタ `Thermal Zone Information` が使えない／読めない PC では、設定上温度が「このPCでは使えません」になる。

## 挙動

1. 従来どおり `Thermal Zone Information` を優先（出典: **システム**）
2. 初期化できない、または有効な値が取れないとき **LibreHardwareMonitorLib** へフォールバック
   - CPU センサ（Package 優先）
   - なければ Motherboard / SuperIO（CPU っぽい名前優先、なければ妥当範囲の最大）
   - UI 上の出典はいずれも **CPU**
3. どちらかで取れれば `IsAvailable = true`

## ログ

`%LocalAppData%\RunCat365\startup.log` に初期化結果・失敗理由を追記する。

## 依存

- NuGet: `LibreHardwareMonitorLib` 0.9.6（MPL-2.0）
- CPU + Motherboard を有効化（GPU 等は未対応）

## 制限

- 機種・ドライバによっては管理者権限やベンダー固有センサが必要
- LHM でも取れない場合は非対応のまま（次候補は WMI など）
