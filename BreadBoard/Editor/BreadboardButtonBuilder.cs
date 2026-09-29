using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;

public static class BreadboardButtonBuilder
{
    const string Root="Assets/BreadBoard";
    static Material MaterialAsset(string name,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Breadboard/Color"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;EditorUtility.SetDirty(m);return m;
    }
    public static void ConfigureCatalog(BreadboardCatalog c,GameObject prefab)
    {
        if(c.kinds.Length<=7) {
        c.kinds=new[]{"Wire","R","C","L","D","Q","Button"};
        c.kindLabels=new[]{"Wire","Resistor","Capacitor","Inductor","Diode","Transistor","Button"};
        c.footprints=new[]{"wire-straight","axial-3p","radial-2p","axial-3p","diode-3p","bjt-3pin","button-2p"};
        }
        var parts=new GameObject[Mathf.Max(7,c.partPrefabs==null?0:c.partPrefabs.Length)];if(c.partPrefabs!=null)System.Array.Copy(c.partPrefabs,parts,c.partPrefabs.Length);parts[6]=prefab;c.partPrefabs=parts;
        UdonSharpEditorUtility.CopyProxyToUdon(c,ProxySerializationPolicy.All);EditorUtility.SetDirty(c);
    }
    [MenuItem("Tools/Breadboard/Install button")]
    public static void Install()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play first");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/model/Breadboard.fbx").transform.Find("Button");
        var original=source.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        if(original.blendShapeCount!=1 || original.GetBlendShapeName(0)!="Push")throw new System.Exception("Unexpected Button BlendShape");
        var mesh=Object.Instantiate(original);mesh.name="Button2Pitch";
        var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)vertices[i]+=new Vector3(.015f,source.localPosition.y,0);
        mesh.vertices=vertices;mesh.RecalculateBounds();
        string path=Root+"/model/Button2Pitch.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
        GameObject prefab;var go=new GameObject("Button");
        try{
            var p=go.AddUdonSharpComponent<BreadboardButtonPart>();var model=new GameObject("Model");model.transform.SetParent(go.transform,false);p.modelRoot=model.transform;
            p.buttonRenderer=model.AddComponent<SkinnedMeshRenderer>();p.buttonRenderer.sharedMesh=mesh;
            p.buttonRenderer.localBounds=new Bounds(new Vector3(.01f,0,0),new Vector3(.026f,.054f,.026f));
            p.normalMaterials=new[]{MaterialAsset("ButtonMetal",new Color(.6f,.63f,.65f)),MaterialAsset("ButtonBody",new Color(.15f,.17f,.2f))};
            p.buttonRenderer.sharedMaterials=p.normalMaterials;
            // An invisible cube on the Udon behaviour's own object is the Interact target.
            p.interactCube=go.AddComponent<BoxCollider>();p.interactCube.center=new Vector3(.01f,.015f,0);p.interactCube.size=new Vector3(.0125f,.011f,.012f);p.interactCube.isTrigger=true;
            go.SetActive(false);UdonSharpEditorUtility.CopyProxyToUdon(p,ProxySerializationPolicy.All);
            var backing=UdonSharpEditorUtility.GetBackingUdonBehaviour(p);backing.interactText="Push button";backing.proximity=1f;
            prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Parts/Button.prefab");
        }finally{Object.DestroyImmediate(go);}
        foreach(var c in Object.FindObjectsOfType<BreadboardCatalog>(true))ConfigureCatalog(c,prefab);
        var board=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/InteractiveBreadboard.prefab");
        try{foreach(var c in board.GetComponentsInChildren<BreadboardCatalog>(true))ConfigureCatalog(c,prefab);PrefabUtility.SaveAsPrefabAsset(board,Root+"/Prefabs/InteractiveBreadboard.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(board);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
