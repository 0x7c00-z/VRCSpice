# Button

- 部品一覧のButtonは2端子、2 pitches。FBXのButtonとPush BlendShapeを使用し、元モデルの高さを維持する。
- 見えないBoxColliderをInteract対象として持つ。配置・穴判定には使用しない。プレビューでは無効。
- Interactで押下開始、同じ手のInputUse解放でOFF。保持中は0.4秒ごとに所有者へ保持を通知する。Erase/Edit時は保持者側のInteractを無効にして編集を優先する。
- 抵抗値は未押下100MΩ、押下0.05Ω。UnityのBlendShape重みは0/100（Blenderの0/1）。
- CircuitDocumentのButtonレコードにvalueSIとして保持し、モデル表示と計算を同じ状態から復元する。MNAには常にR_cXXXXXXという固定名で出力する。端子名も押下前後で変えない。
- 動的Prefab自体にはネットワークIDやVRCObjectSyncを持たせず、シーンにあるBreadboardSync経由で所有者へ要求する。所有者がJSON全体を再送するので途中参加にも状態を渡せる。
- 同じボタンを複数人が押した場合は最初に受理された人が解放するまで他の人の要求を無視する。操作番号で遅い解放・解放後の古い保持通知を除外する。
- 保持通知が2秒途絶えた場合や操作した人が退出した場合は解放する。所有権移譲中は要求を保留せず無視し、新所有者では未更新の押下を解放する。保持通知により必要なら再押下される。
- 連打・短いタップはネットワーク状況により他クライアントに中間状態が見えず、最新の解放状態だけが届く場合がある。

`Tools > Breadboard > Install button` はモデル・Prefab・Catalogを更新する。`Verify button data and model` はJSON、抵抗入力、BlendShape、元の高さを検証する。

検証結果：モデル・データ327項目、既存データ674項目、動的生成53項目、押下入力・通知順序・タイムアウト・削除11項目が通過。Playモードはコンパイル済みUdonで確認。実VRChatでの複数クライアント通信は未検証。ClientSimの既存SDK初期化例外は残っている。
