# これは何
VRC上で動作するSPICE(回路網シミュレーター)です。

# setup
空のUnity 2022 world projectを作り、
```
rm -rf Assets
git clone https://github.com/0x7c00-z/VRCSpice.git Assets
```
その後、

・VCCからQvPenとYamaplayerをimport
・UnityでAssets/Scene.unityを開く
・Textmesh pro essentialsをimport

#その他
・回路は、Breadboardをいじったタイミング or 回路を入力し、Restart Simを押したタイミングでロードされる。

