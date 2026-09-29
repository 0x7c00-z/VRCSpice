# LED

LEDは1 pitchの2端子。FBXのLEDを使い、元の高さを維持する。樹脂の平らな側をK（カソード）とし、基準穴はA（アノード）、向きに沿って1 pitch先をKとする。

- 部品種別LEDを追加し、MNAにはD_cXXXXXXとして渡す。7個のダイオード定数を使う。
- 利用者指定のAOT-2015モデルからIs=5.961e-10 A、N=6.222、Cjo=0.5nFを反映。Vtは既存の熱電圧0.02585 Vを使いN×0.02585=0.1608387 Vへ換算する。TT=2e-9 s、Vj=0.6 V、m=Fc=0.5は維持。現行7引数にはRs、Xti、Iave、Vpkの入力先がないため、それらは未反映。互換性のためモデルIDはled-red-provisionalのまま。
- 発光は赤固定。将来の色違いは別コンポーネントとして追加する。
- Base Color＋発光色×max(順方向電流,0)×100を出力する。発光部は不透明描画で、消灯時もBase Color（初期値は暗い赤）を表示する。HDR値を保持し、20mAでは赤成分2。ブルームや周辺物体を照らすLightは追加しない。
- 電流はSolver.hlslのget_data(0, currentRow)で読む。現在のAPIは履歴位置、ベクトル行番号の順。CPUへの読み戻しは行わない。
- BreadboardMnaAdapterが更新後にBreadboardRenderer経由で各partのBindSolverを呼ぶ。行番号はI_D_cXXXXXX_0をlabel2bufferRowへ渡して取得する。PartにSolverのInspector参照は不要。
- 同一ボードでは発光マテリアルを共有し、各LEDの行番号のみMaterialPropertyBlockで上書きする。別ボードでは別マテリアルにしてSolver間の干渉を防ぐ。
- BreadboardRendererのLateUpdateでWriteToMaterialを呼び、Solverのダブルバッファ切替を追従する。
- 編集直後は行番号を無効化し、MNA更新後に再解決する。プレビュー、未接続Solver、見つからない行番号、非有限値、負電流では発光しない。
- LED削除時はRendererごとの上書きを解除し、共有マテリアルはボード破棄時に破棄する。

`Tools > Breadboard > Install LED` でモデルとPrefab、シーンとボードPrefabのCatalogを更新する。`Verify LED` は部品データと既知のGPU電流による発光を検証する。

検証済み：JSON往復・MNAへの7定数、GPU上の5mA/20mAで発光強度4倍、負電流・無効行の消灯、共有MaterialとRenderer別の行番号。コンパイル済みUdonで2個のLEDが16/17行を参照し、片方の削除後に残ったLEDが17→16行へ再解決されることを確認。既存データ検証と動的生成検証も通過。実VRChatでの複数クライアント確認は未実施。

Base ColorはMaterials/LedEmitter.matで変更できる。透明樹脂は従来どおり別マテリアルで描画する。
