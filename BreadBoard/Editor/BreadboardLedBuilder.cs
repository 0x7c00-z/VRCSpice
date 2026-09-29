using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;

public static class BreadboardLedBuilder
{
    const string Root="Assets/BreadBoard";
    static Material Mat(string name,string shader,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
        if(m.HasProperty("_Color"))m.color=color;EditorUtility.SetDirty(m);return m;
    }
    public static void Configure(BreadboardCatalog c,GameObject prefab)
    {
        c.kinds=new[]{"Wire","R","C","L","D","Q","Button","LED"};
        c.kindLabels=new[]{"Wire","Resistor","Capacitor","Inductor","Diode","Transistor","Button","LED"};
        c.footprints=new[]{"wire-straight","axial-3p","radial-2p","axial-3p","diode-3p","bjt-3pin","button-2p","led-1p"};
        System.Array.Resize(ref c.partPrefabs,8);c.partPrefabs[7]=prefab;
        if(c.ModelIndex("led-red-provisional",7)<0)
        {
            int i=c.modelIds.Length;
            System.Array.Resize(ref c.modelIds,i+1);System.Array.Resize(ref c.modelLabels,i+1);System.Array.Resize(ref c.modelKinds,i+1);
            System.Array.Resize(ref c.diodeIs,i+1);System.Array.Resize(ref c.diodeVt,i+1);System.Array.Resize(ref c.diodeTT,i+1);System.Array.Resize(ref c.diodeCjo,i+1);System.Array.Resize(ref c.diodeVj,i+1);System.Array.Resize(ref c.diodeM,i+1);System.Array.Resize(ref c.diodeFc,i+1);
            c.modelIds[i]="led-red-provisional";c.modelLabels[i]="Red LED (provisional)";c.modelKinds[i]=7;
            c.diodeIs[i]=5.961e-10f;c.diodeVt[i]=.1608387f;c.diodeTT[i]=2e-9f;c.diodeCjo[i]=5e-10f;c.diodeVj[i]=.6f;c.diodeM[i]=.5f;c.diodeFc[i]=.5f;
        }
        UdonSharpEditorUtility.CopyProxyToUdon(c,ProxySerializationPolicy.All);EditorUtility.SetDirty(c);
    }
    [MenuItem("Tools/Breadboard/Install LED")]
    public static void Install()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play first");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/model/Breadboard.fbx").transform.Find("LED");
        var original=source.GetComponent<MeshFilter>().sharedMesh;
        var sourceMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
        var mesh=Object.Instantiate(original);mesh.name="Led1Pitch";
        // Flat side (-X in FBX) is cathode. Anchor is anode (+X); keep original world height.
        var v=mesh.vertices;var n=mesh.normals;var t=mesh.tangents;
        for(int i=0;i<v.Length;i++) {v[i]=new Vector3(.005f-v[i].x,v[i].y+source.localPosition.y,-v[i].z);n[i]=new Vector3(-n[i].x,n[i].y,-n[i].z);if(t.Length==v.Length)t[i]=new Vector4(-t[i].x,t[i].y,-t[i].z,t[i].w);}
        mesh.vertices=v;mesh.normals=n;mesh.tangents=t;mesh.RecalculateBounds();
        string path=Root+"/model/Led1Pitch.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject("LED");GameObject prefab;
        try{
            var p=go.AddUdonSharpComponent<BreadboardLedPart>();var model=new GameObject("Model");model.transform.SetParent(go.transform,false);p.modelRoot=model.transform;
            model.AddComponent<MeshFilter>().sharedMesh=mesh;p.ledRenderer=model.AddComponent<MeshRenderer>();
            p.normalMaterials=new Material[sourceMaterials.Length];p.emitterSlot=-1;
            for(int i=0;i<sourceMaterials.Length;i++) {
                string name=sourceMaterials[i].name;
                if(name=="Metal")p.normalMaterials[i]=Mat("LedMetal","Breadboard/Color",new Color(.65f,.68f,.7f));
                else if(name=="LED_Resin_Clear")p.normalMaterials[i]=Mat("LedResin","Breadboard/Preview",new Color(.9f,.93f,1,.15f));
                else if(name=="LED_Emitter") {p.emitterSlot=i;p.normalMaterials[i]=Mat("LedEmitter","Breadboard/LED Current Emission",Color.white);}
                else throw new System.Exception("Unknown LED material: "+name);
            }
            if(p.emitterSlot<0)throw new System.Exception("LED emitter missing");
            p.emitterTemplate=p.normalMaterials[p.emitterSlot];p.emitterRenderer=p.ledRenderer;
            p.ledRenderer.sharedMaterials=p.normalMaterials;go.SetActive(false);UdonSharpEditorUtility.CopyProxyToUdon(p,ProxySerializationPolicy.All);
            prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Parts/LED.prefab");
        }finally{Object.DestroyImmediate(go);}
        foreach(var c in Object.FindObjectsOfType<BreadboardCatalog>(true))Configure(c,prefab);
        var board=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/InteractiveBreadboard.prefab");
        try{foreach(var c in board.GetComponentsInChildren<BreadboardCatalog>(true))Configure(c,prefab);PrefabUtility.SaveAsPrefabAsset(board,Root+"/Prefabs/InteractiveBreadboard.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(board);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
