# インタラクティブ・ブレッドボード

VRChat内で、片手に持ったブレッドボードへ反対の手で部品を配置し、穴の接続から回路を生成して既存のMNAシミュレーターへ渡す。

電源はボードの電源ラインに備え付けとする。Breadboard側は配置・同期・導通の統合・MNAへの入力を担当し、回路の有効性判定と波形の保持・表示はMNA側に任せる。非線形モデルの入力拡張は後からMNA側で実装できる境界を残す。

初期実装を `Scene.unity` の既存Breadboardに組み込み済み。既存の `model/Breadboard.fbx` を使用し、400穴の配置定義、部品プール、操作パネル、JSON同期、MNA入力生成を分離している。

- [使い方・実装状況](Docs/Implementation.md)：操作、Inspector設定、Prefab、確認結果と未検証事項。

- [要件定義](Docs/Requirements.md)：操作、部品、固定電源、同期、MNA連携、初期案。
- [モジュール設計](Docs/Modules.md)：責務・依存関係、既存コードからの移行。
- [データ仕様](Docs/DataContract.md)：穴ID、回路JSON、導通の統合、MNAへの変換。
- [受け入れ条件](Docs/Acceptance.md)：実装完了を確認するシナリオ。

関連リソースの配置先は `Assets/Breadboard`。既存ディレクトリの表記が `Assets/BreadBoard` であり、このWindows環境では同じ場所を指すため、現在の表記と既存アセットのGUIDを維持する。新しい資料もこの配下に置く。将来の大文字・小文字を区別する環境では、既存の `BreadBoard` 表記に統一する。

`model/`、`Board.cs`、`ComponentManager.cs` は既存アセットで、ファイルは維持している。シーンの旧Boardコンポーネントは新モジュールへ置き換え、旧Net入力UIを非表示にした。既存Testのオシロスコープ操作は維持する。シミュレーター本体は引き続き `Assets/MNATools` に置き、連携部分だけを本フォルダーに置く。
