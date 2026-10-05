# 説明パネル

Hierarchy の `instruction > InstructionPanel` を選択すると、Inspector に「項目数」「項目名」「本文」が表示されます。

- 項目数を増減してページを追加・削除します。減らすと末尾の項目が消えます。
- 各項目名が Dropdown の選択肢になります。本文には改行や `<b>太字</b>` などの Rich Text が使えます。
- 本文が長い場合は縦スクロールできます。ページ切替時は先頭に戻ります。
- 編集後はシーンを保存してください。実行中の編集は想定していません。
- 選択状態はプレイヤーごとです。他プレイヤーには同期しません。

`InstructionPanel.prefab` は初期状態の再利用用です。既存シーン側の文章編集はこのPrefabには自動反映されません。
日本語用の Zen Maru Gothic を同梱しています。ライセンスは OFL.txt を参照してください。

本文・選択欄は TextMeshPro です。`ZenMaruGothic TMP.asset` は日本語対応の Dynamic / Multi Atlas フォントで、追加した文章の文字も生成できます。
文字サイズは各オブジェクトの TextMeshPro - Text (UI) の Font Size で変更します。
- 本文: `Scroll View/Viewport/Content`
- 選択中の項目: `Dropdown/Label`
- 開いた選択肢: `Dropdown/Template/Viewport/Content/Item/Item Label`
