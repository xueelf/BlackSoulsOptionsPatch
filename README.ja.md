# BlackSoulsQoLPatch

ゲーム [BLACK SOULS](https://www.dlsite.com/maniax/work/=/product_id/RJ203687.html) と [BLACK SOULS II](https://www.dlsite.com/maniax/work/=/product_id/RJ237469.html) の非公式 QoL 最適化パッチで、プログラム内に設定画面や戦闘ログを追加します。ウィンドウサイズと全画面表示を変更でき、BGM、BGS、ME、SE の音量を個別に調整できます。

他の言語で読む：[English](./README.md) | [中文](./README.zh.md) | 日本語

## 機能

- 設定画面：タイトル画面に「OPTIONS」、ポーズメニューに「オプション」の項目を追加します。画面は RPG Maker MZ を参考にしています。
- ウィンドウサイズ：640×480、1280×960、1920×1440、2560×1920 の 4 種類に対応します。
- ボーダーレス全画面：全画面ではゲーム本来の 4:3 画面を画面比率に合わせて引き伸ばします。
- 音量調整：BGM、BGS、ME、SE の音量を個別に調整できます。
- ダッシュ動作：初期状態はオフ。オンにするとキャラクターは通常ダッシュで移動し、Shift キーを押すと歩行します。
- 戦闘ログ：戦闘コマンドの「ログ」から、現在の戦闘履歴を確認できます。
- DPI スケーリング：`Game.exe` の Windows 高 DPI 互換性を有効にし、表示スケーリングによる画面の引き伸ばしを防ぎます。
- 設定の保存：設定はゲームディレクトリの `Options.ini` ファイルに保存されます。

## ダウンロード

[GitHub Releases](https://github.com/xueelf/BlackSoulsOptionsPatch/releases/latest) から使用するゲームの exe をダウンロードしてください：

BLACK SOULS：

- 中国語：`BlackSouls-QoL-CN.exe`
- 日本語：`BlackSouls-QoL-JP.exe`
- 英語：`BlackSouls-QoL-EN.exe`

BLACK SOULS II：

- 中国語：`BlackSoulsII-QoL-CN.exe`
- 日本語：`BlackSoulsII-QoL-JP.exe`
- 英語：`BlackSoulsII-QoL-EN.exe`

## 使い方

1. ゲームを終了します。
2. 使用するゲームの exe をゲームディレクトリに置き、`Game.exe`、`Game.rgss3a` と同じ場所にします。
3. ダウンロードした exe をダブルクリックし、番号を選択します：

```text
1) インストール/更新
2) アンインストール
3) 終了
```

インストール時にゲーム原本の `Game.rgss3a` を `Game.rgss3a.bsopts.bak` としてバックアップします。アンインストールすると原本に戻し、バックアップと `Options.ini` を削除し、高 DPI 互換性設定を解除します。

ゲームディレクトリに exe がない場合、または `Game.exe`、`Game.rgss3a` が欠けている場合、プログラムはエラーメッセージを表示します。

## 声明

パッチはゲームスクリプトを変更し、exe アイコンはゲーム本体の素材を使用しており、著作権はそれぞれの権利者に帰属します。本プロジェクトは学習目的のみですので、宣伝や悪用はしないでください。正規版ゲームを購入してご支援ください。
