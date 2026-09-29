# ワイヤーモデル表示

`Prefabs/Parts/Wire.prefab` は `BreadboardWirePart : BreadboardPart` を使う。通常の線・ラベルをFBXのWireモデルへ置き換え、SkinnedMeshRendererのBlendShapeで長さを変更する。

- 新規配置・長さ編集は1〜9 pitches。両端選択で9を超える場合は無効プレビューと理由を表示し、確定しない。
- 長さ1〜9の被覆色は茶・赤・橙・黄・緑・青・紫・灰・白。金属部分は共通の金属マテリアル。抵抗用の既存カラーマテリアルを共有する。
- `Length(1-10)` の重みは `(length - 1) * 100 / 9`。9 pitchesは約88.89。モデルを一方向へ拡大せず、端子の太さを保つ。
- プレビューは金属・被覆とも既存の配置可能／不可用半透明マテリアルを使う。
- 保存済みの10 pitches以上の配線は従来の線とラベルで表示し、接続を維持する。読み込み・同期・MNAの長さ制約は変更しない。

## FBX更新

`Tools > Breadboard > Install modeled wire` で `model/Breadboard.fbx` のWireから派生メッシュ `model/WireLength.asset` とWire.prefabを再生成する。PrefabのGUIDを維持するのでCatalogの参照変更は不要。

現FBXの固定端子中心は(-.005,-.025,-.005)、可動端子中心は(-.015,-.025,-.005)。BuilderはY軸180度回転と平行移動で固定端子の水平位置を原点、伸長方向を+Xに合わせる。高さは元モデルを維持し、25mmの持ち上げ補正は行わない。リード先端はボード表面より25mm下に入る。BlendShapeの頂点・法線・接線差分も同じ回転を適用する。FBXの軸や寸法を変えた場合は変換を見直す。カリング用Boundsは最大伸長を含める。

## 検証

`Tools > Breadboard > Verify modeled wire`：76項目。1〜9 pitches×4方向の端子位置・色、長い配線の従来表示と復帰、プレビュー両色。

既存データ検証674項目とPlayモード動的表示47項目を通過。コンパイル済みUdon上でも1〜9の伸長・色、12 pitchesの従来表示、無効プレビューへの復帰を確認した。実VRChatでの複数クライアント確認は未実施。
