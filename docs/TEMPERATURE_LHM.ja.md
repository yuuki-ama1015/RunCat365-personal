# 温度取得と LibreHardwareMonitor（LHM）

## 背景

Windows のパフォーマンスカウンタ `Thermal Zone Information` が使えない／読めない PC では、設定上温度が「このPCでは使えません」になる。

## Phase 1 の挙動

1. 従来どおり `Thermal Zone Information` を優先
2. 初期化できない、または有効な値が取れないとき **LibreHardwareMonitorLib** で CPU 温度（Package 優先、なければコア最大）へフォールバック
3. どちらかで取れれば `IsAvailable = true`

## 依存

- NuGet: `LibreHardwareMonitorLib` 0.9.6（MPL-2.0）
- CPU センサのみ有効化（GPU 等は Phase 外）

## 制限

- 機種・ドライバによっては管理者権限やベンダー固有センサが必要
- LHM でも取れない場合は非対応のまま
