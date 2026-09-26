# 部品Prefabと基底クラス

## 実装した範囲

表示管理は `BreadboardRenderer`、個々の表示は `BreadboardPart : UdonSharpBehaviour` の派生クラスが担当する。現在は `BreadboardSimplePart` が従来の簡易形状と文字を描画する。カラーコード・LED発光・スイッチ入力やBlendShape・MNAの定数更新は今回の範囲外。

- Catalogの `partPrefabs` はkindsと同じ順序のGameObject配列。
- 確定部品は安定した部品IDに紐付ける。Stateの配列位置が削除や受信で変わっても同じIDの表示を再利用する。
- 新IDは生成、既存IDは更新、消えたIDは非表示にして破棄する。同じIDでも種別が変わった場合は交換する。
- プレビューは同じ種別なら再利用し、種別変更時に交換する。非表示のプレビューを最大1個保持する。
- 空の回路では確定部品の表示GameObjectは0個。容量分の参照配列だけを確保する。データ容量128と表示生成方式は独立。
- 生成は各クライアントのローカル処理。回路JSON・穴占有・所有権の仕組みは変更していない。Collider、Pickup、VRCObjectSyncは部品に付けない。

## ライフサイクル

1. マネージャーがPrefabをInstantiateする。現行SDKのUdonSharpではこの呼出しがローカル生成へ変換される。
2. ボードと同じ座標系のParts以下に親子付けし、位置0・回転identity・scale1へ設定。
3. `InitializePart(layout, catalog, placement, id, preview, validMaterial, invalidMaterial)` を呼ぶ。
4. `ApplyState(kind, anchor, orientation, length, value, model, valid)` を呼んで表示し、GameObjectを有効化。
5. 状態変更時は同じインスタンスのApplyStateを呼ぶ。
6. 削除・交換時は無効化後に `ReleasePart()` を呼び、GameObjectをDestroyする。

生成後の参照設定は明示的に行う。Start/OnEnableの実行順に初期化を依存させない。初期化前にもイベントが来る可能性を考慮し、派生クラスで必要な場合は `initialized` を確認する。

## 新しいモデルへの差し替え

- PrefabのルートにBreadboardPartを継承した具象クラスを1つ付ける。中のRendererやSkinnedMeshRenderer等はInspector参照として保持する。
- Prefabは非アクティブで保存する。ルートのTransformは規定値とし、モデル固有のオフセット・回転・スケールは子に設定する。
- 基底クラスの型で呼ばれるvirtualメソッドをoverrideする。InitializePartをoverrideする場合はbase.InitializePartも呼ぶ。
- ApplyStateは表示処理のみを行い、回路や同期状態を変更しない。isPreview時は半透明表示とし、将来の操作機能も無効にする。半透明化は派生クラスの責務で、SimplePartでは本体とリードへ適用済み。
- 基底クラスのlayout/placementからボードローカルの端子位置を取得できる。Prefabの子の位置も同じ座標系で設定する。
- インスタンス専用Material等を作る派生クラスはReleasePartで解放する。共通アセットは破棄しない。
- 新しい派生スクリプトはUdonプログラムとしてコンパイルし、PrefabをCatalog.partPrefabsへ登録する。

既存のbodyMeshes/bodyMaterials/leadMaterialsは仮表示SimplePartと初期Prefab作成に使用する。完成モデル用の派生クラスはこれらに依存する必要はない。ピン配置のデータ化は今回行っていない。

## Editorと検証

`Tools > Breadboard > Upgrade prefab to dynamic parts` はInteractiveBreadboardの旧Partsプールを空の生成先へ置き換える。登録済みPrefab・既存の部品Prefabファイルは上書きしない。現在のシーンには適用済み。

`Tools > Breadboard > Verify dynamic parts (Play mode)` はテスト用回路を作ってコンパイル済みUdonを実行する。試験中は同期と手入力を停止するため、終了時はPlayを停止すること。編集モードの `Verify data pipeline` とは別の検証。
