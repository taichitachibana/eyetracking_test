# 視線・瞬きデータ分析ガイド

## 1. 分析の目的

本実験では、主に次の問いを検証する。

1. `VignetteOn` と `VignetteOff` で視線の安定性が異なるか。
2. Vignetteの有無によって瞬きの頻度や持続時間が異なるか。
3. 外乱中に視線が不安定になるか。
4. Vignetteが外乱による視線の乱れを軽減するか。
5. 条件間でアイトラッキングの計測品質に差がないか。

分析では、0.5秒ごとの値をそのまま独立した参加者として扱わず、参加者・記録・条件のまとまりを考慮する。

## 2. 使用するCSVファイル

記録を開始するたびに、同じ日時を含む次のCSVファイルが生成される。

| ファイル | 内容 | 主な用途 |
| --- | --- | --- |
| `*_experiment_events.csv` | 記録開始・終了、条件変更、外乱開始・終了 | 分析区間の特定 |
| `*_gaze_summary.csv` | 0.5秒窓ごとの視線平均、分散、追跡成否 | 条件比較の中心データ |
| `*_blink_events.csv` | 瞬きとトラッキング失敗イベント | 瞬き分析 |
| `*_gaze_raw.csv` | 約60 Hzの視線、頭部移動、閉眼度 | 詳細な時系列確認・異常調査 |

Unity Editorで実行した場合、ファイルは原則として次の場所に保存される。

```text
Assets/data/
```

実機ビルドでは、`Application.persistentDataPath/data` に保存される。

## 3. 分析前に準備する情報

CSVだけでは参加者を確実に識別できないため、別途、次の対応表を作成する。

```csv
ParticipantId,FileStamp,Condition,Order
P001,20260804_101500,VignetteOff,1
P001,20260804_102000,VignetteOn,2
P002,20260804_110000,VignetteOn,1
P002,20260804_110500,VignetteOff,2
```

- `ParticipantId`：匿名化した参加者ID
- `FileStamp`：CSVファイル名先頭の日時
- `Condition`：`VignetteOn` または `VignetteOff`
- `Order`：その参加者が条件を体験した順番

順序効果を確認できるように、`Order` は必ず残す。

## 4. 分析の全体手順

1. ファイルと参加者IDの対応表を作る。
2. 記録が正常に開始・終了しているか確認する。
3. 追跡有効率を計算し、データ品質を確認する。
4. 外乱前・外乱中・外乱後の区間を付与する。
5. 視線指標を参加者・条件・区間ごとに集計する。
6. 瞬き指標を参加者・条件・区間ごとに集計する。
7. グラフで分布、外れ値、時系列を確認する。
8. 実験計画に対応した統計検定を行う。
9. 除外基準、効果量、信頼区間を含めて結果を報告する。

## 5. 記録の完全性を確認する

`experiment_events.csv` を開き、各記録に次のイベントが存在することを確認する。

- `RecordingStart`
- `RecordingStop`
- 必要に応じて `DisturbanceStart`
- 必要に応じて `DisturbanceEnd`

`RecordingStop` がない場合、アプリの強制終了などが疑われる。その記録を直ちに削除せず、実際の記録時間と他のCSVの末尾を確認して判断する。

`Condition` が `Unspecified` の記録は、実験記録や対応表から条件を復元できない限り、条件比較には使用しない。

## 6. データ品質の確認

### 6.1 視線追跡有効率

`gaze_summary.csv` の各行について、次を計算する。

```text
ValidRate = ValidSampleCount / SampleCount
TrackingFailureRate = TrackingFailureCount / SampleCount
```

ゼロ除算を防ぐため、`SampleCount == 0` の行は欠損値として扱う。

記録全体の有効率は、行ごとの有効率の単純平均ではなく、次のようにサンプル数から計算する。

```text
RecordingValidRate = sum(ValidSampleCount) / sum(SampleCount)
```

### 6.2 除外基準

分析開始後に都合よく基準を変えないよう、除外基準は条件間の検定前に決定する。候補例は次のとおり。

- 記録全体の有効率が80%未満
- 有効サンプルがない窓
- 記録時間が予定時間から大きく外れている
- 条件が特定できない
- 機器トラブルや実験手順の逸脱が記録されている

80%は固定的な正解ではない。全参加者の有効率分布を確認し、採用した基準と除外件数を報告する。

条件によって有効率が異なる場合、視線指標の差が計測品質の差によって生じている可能性がある。そのため、`RecordingValidRate` 自体も条件間で比較する。

## 7. 外乱区間を付与する

`experiment_events.csv` から `DisturbanceStart` と `DisturbanceEnd` の `ElapsedSec` を取得する。

`gaze_summary.csv` の各窓を、次のように分類する。

```text
Pre   : WindowEndElapsedSec <= DisturbanceStart
During: 外乱区間と窓が重なっている
Post  : WindowStartElapsedSec >= DisturbanceEnd
```

外乱の境界をまたぐ0.5秒窓には、次のいずれかの規則をあらかじめ適用する。

- 窓の中央時刻が属する区間に割り当てる。
- 境界をまたぐ窓を分析から除外する。

厳密に区間を分けたい場合は、境界をまたぐ窓を除外する方法が分かりやすい。

外乱イベントが複数回ある場合は、各外乱に `DisturbanceId` を付ける。

## 8. 視線データの分析

### 8.1 使用する指標

`gaze_summary.csv` から次の指標を使用する。

| 指標 | 意味 | 解釈 |
| --- | --- | --- |
| `MeanGazeX` | 画面中心からの水平方向の平均ずれ | 0に近いほど中央 |
| `MeanGazeY` | 画面中心からの垂直方向の平均ずれ | 0に近いほど中央 |
| `VarianceGazeX` | 水平方向の視線分散 | 小さいほど安定 |
| `VarianceGazeY` | 垂直方向の視線分散 | 小さいほど安定 |
| `Dispersion` | 水平・垂直分散をまとめた散らばり | 小さいほど安定 |
| `ValidSampleCount` | 有効な視線サンプル数 | 品質確認に使用 |

画面中心からの平均距離を追加で計算する。

```text
CenterDistance = sqrt(MeanGazeX^2 + MeanGazeY^2)
```

`MeanGazeX`、`MeanGazeY`、`Dispersion` はViewport座標に基づく。現在の値は視角の度数ではないため、異なるFOVや投影条件のデータを直接比較するときは注意する。

### 8.2 集計単位

最低限、次の単位で集計表を作成する。

```text
ParticipantId × Condition × Phase
```

各単位について以下を計算する。

- `Dispersion` の平均と中央値
- `CenterDistance` の平均と中央値
- `VarianceGazeX` と `VarianceGazeY` の平均
- `ValidSampleCount` の合計
- `SampleCount` の合計
- 有効率
- 使用した窓数

分散指標は右に裾の長い分布になる可能性があるため、平均だけでなく中央値も保存する。

### 8.3 時系列分析

外乱への反応を見る場合は、外乱開始時刻を0秒として相対時刻を作る。

```text
RelativeTime = WindowMidpoint - DisturbanceStart
WindowMidpoint = (WindowStartElapsedSec + WindowEndElapsedSec) / 2
```

参加者ごとに相対時刻を揃えた後、条件別に平均時系列と95%信頼区間を描く。

## 9. 瞬きデータの分析

### 9.1 分析対象の抽出

`blink_events.csv` から、原則として次の行だけを瞬きとして採用する。

```text
EventType == "Blink"
IsAccepted == 1
TrackingInterrupted == 0
```

次の行は瞬き回数に含めない。

- `EventType == "TrackingFailure"`
- `IsAccepted == 0`
- `TrackingInterrupted == 1`

追跡中断を伴う瞬きを残す場合は、主要分析とは別の感度分析として扱う。

### 9.2 瞬き指標

参加者・条件・区間ごとに次を計算する。

```text
BlinkCount = 採用された瞬き数
BlinkRatePerMin = BlinkCount / 有効な区間時間（分）
MeanBlinkDuration = mean(DurationMs)
MedianBlinkDuration = median(DurationMs)
MeanMaxClosure = mean(MaxClosure)
MeanClosure = mean(MeanClosure)
```

条件ごとに記録時間が違う可能性があるため、単純な瞬き回数だけでなく、1分あたりの瞬き率を主要指標とする。

瞬きの区間分類には、原則として瞬き開始時刻 `StartElapsedSec` を使用する。境界付近の瞬きについては、開始時刻または中点のどちらを使うか事前に決める。

## 10. `gaze_raw.csv` の使用方法

生データは主要な条件比較よりも、次の目的に使用する。

- 視線軌跡の描画
- 外れ値や急激なジャンプの確認
- 頭部移動と視線移動の関係確認
- 瞬き検出が閉眼度と一致するかの確認
- 要約データで異常が見つかった区間の調査

主な列は次のとおり。

| 列 | 内容 |
| --- | --- |
| `HeadDeltaX/Y/Z` | 記録開始時からの頭部位置変化 |
| `GazeCorrX/Y` | 頭部回転補正後の視線位置 |
| `IsBlink` | 閉眼度が閾値以上なら1 |
| `BlinkStatus` | 瞬き判定または取得失敗の状態 |
| `LeftEyeClosure` | 左目の閉眼度 |
| `RightEyeClosure` | 右目の閉眼度 |
| `EyeClosureMean` | 左右の平均閉眼度 |

`gaze_raw.csv` の `IsBlink` はフレーム単位の閾値判定であり、瞬きイベント数ではない。瞬き回数と持続時間には `blink_events.csv` を使用する。

## 11. 可視化

最低限、次のグラフを作成する。

1. 参加者ごとの記録有効率
2. 条件別の `Dispersion` の箱ひげ図またはバイオリンプロット
3. 条件別の `CenterDistance` の分布
4. 外乱開始を0秒に揃えた `Dispersion` の時系列
5. 条件別の1分あたり瞬き率
6. 条件別の瞬き持続時間

対応のある実験では、条件別の棒グラフだけでなく、同一参加者の2条件を線で結ぶ図を使用する。これにより個人差と条件差を区別しやすくなる。

## 12. 統計解析

### 12.1 2条件のみを比較する場合

同じ参加者が `VignetteOn` と `VignetteOff` の両方を体験する場合は、対応のある比較を行う。

- 差分がおおむね正規分布：対応のあるt検定
- 差分の正規性が乏しい、外れ値が強い：Wilcoxon符号付順位検定

対象例：

- 参加者ごとの平均 `Dispersion`
- 参加者ごとの平均 `CenterDistance`
- 参加者ごとの `BlinkRatePerMin`
- 参加者ごとの追跡有効率

### 12.2 条件と外乱区間を同時に比較する場合

次の2要因を評価する。

```text
Condition: VignetteOn / VignetteOff
Phase: Pre / During / Post
```

候補となる手法は次のとおり。

- 反復測定ANOVA
- 線形混合効果モデル

欠測や参加者ごとの窓数の違いがある場合は、混合効果モデルが扱いやすい。

概念的なモデルは次のようになる。

```text
Dispersion ~ Condition * Phase + Order + (1 | ParticipantId)
```

特に確認したいのは `Condition × Phase` の交互作用である。交互作用が認められれば、外乱による変化がVignetteの有無によって異なる可能性を示す。

`Dispersion` の分布が強く歪む場合は、対数変換または分布に適したモデルを検討する。値が0の場合は、変換前に小さな定数を加える方法を明記する。

### 12.3 瞬き回数をモデル化する場合

瞬き回数は連続量ではなくカウントデータである。記録時間が異なる場合は、Poisson回帰または負の二項回帰で記録時間をオフセットにする方法も使用できる。

```text
BlinkCount ~ Condition * Phase + offset(log(DurationMin))
```

### 12.4 報告する値

p値だけでなく、次を報告する。

- 各条件の平均値または中央値
- 条件差
- 95%信頼区間
- 効果量
- 使用した参加者数
- 除外した参加者・記録・窓の数と理由
- 使用した検定またはモデル

対応のあるt検定では対応ありの効果量、ノンパラメトリック検定では順位に基づく効果量など、検定に合った効果量を用いる。

## 13. 避けるべき分析

- 0.5秒窓をすべて独立サンプルとして通常のt検定に投入する。
- 追跡失敗を0の視線値として扱う。
- `TrackingFailure` を瞬きとして数える。
- 記録時間が違う条件を瞬き回数だけで比較する。
- 条件を見た後で除外基準を変更する。
- 平均値だけを確認し、参加者ごとの分布や外れ値を確認しない。
- Viewport座標のDispersionを、変換せずに視角の度数として解釈する。

## 14. 推奨する出力ファイル

再現性を確保するため、分析後は次のファイルを保存する。

```text
analysis/
├── participant_file_map.csv
├── exclusion_log.csv
├── gaze_window_clean.csv
├── gaze_participant_summary.csv
├── blink_clean.csv
├── blink_participant_summary.csv
├── figures/
└── statistical_results.csv
```

`exclusion_log.csv` には、除外対象、理由、適用した基準を記録する。

## 15. 最終チェックリスト

- [ ] 全ファイルに参加者IDを対応付けた。
- [ ] 条件が `Unspecified` の記録を確認した。
- [ ] 記録開始・終了イベントを確認した。
- [ ] 外乱開始・終了時刻を確認した。
- [ ] 追跡有効率を計算した。
- [ ] 除外基準を条件比較前に決定した。
- [ ] 外乱前・外乱中・外乱後を付与した。
- [ ] 瞬きと追跡失敗を区別した。
- [ ] 参加者単位の集計表を作成した。
- [ ] 同一参加者内の対応を考慮した統計手法を選んだ。
- [ ] 効果量と95%信頼区間を算出した。
- [ ] 除外件数と理由を記録した。

## 16. 現在の記録形式に関する注意

`gaze_raw.csv` には、現状では `SessionId`、`RecordingId`、`Condition`、共通形式の `ElapsedSec` が含まれていない。ファイル名と `AbsTime` から他のCSVに対応付けられるが、取り違えを防ぐため、解析時には必ず `participant_file_map.csv` を作成する。

今後データ収集を続ける場合は、生データにも次の列を追加することが望ましい。

```text
ParticipantId
SessionId
RecordingId
Condition
ElapsedSec
```

参加者IDをUnity側で入力しない場合でも、少なくとも収集直後にファイル名と参加者IDの対応を記録する。
