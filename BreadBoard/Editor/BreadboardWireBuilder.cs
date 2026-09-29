using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using TMPro;

public static class BreadboardWireBuilder
{
    const string Root = "Assets/BreadBoard";
    [MenuItem("Tools/Breadboard/Install modeled wire")]
    public static void Install()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Exit Play first.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/model/Breadboard.fbx").transform.Find("Wire").GetComponent<SkinnedMeshRenderer>();
        var original = source.sharedMesh;
        if(original.blendShapeCount != 1 || original.GetBlendShapeName(0) != "Length(1-10)" || original.subMeshCount != 2)
            throw new System.Exception("Unexpected Wire mesh / BlendShape layout");
        int metal = -1, color = -1;
        for(int i=0;i<source.sharedMaterials.Length;i++) { if(source.sharedMaterials[i].name=="Metal")metal=i; if(source.sharedMaterials[i].name=="color0")color=i; }
        if(metal<0 || color<0) throw new System.Exception("Wire materials missing");
        // FBX pin centers: (-.005,-.025,-.005) and (-.015,-.025,-.005).
        // Rotate 180 degrees around Y and align the fixed pin horizontally; preserve source height.
        var mesh = Object.Instantiate(original); mesh.name="WireLength";
        var v=mesh.vertices;var n=mesh.normals;var tangents=mesh.tangents;
        for(int i=0;i<v.Length;i++) {
            v[i]=new Vector3(-v[i].x-.005f,v[i].y,-v[i].z-.005f);
            if(n.Length==v.Length)n[i]=new Vector3(-n[i].x,n[i].y,-n[i].z);
            if(tangents.Length==v.Length)tangents[i]=new Vector4(-tangents[i].x,tangents[i].y,-tangents[i].z,tangents[i].w);
        }
        mesh.vertices=v;mesh.normals=n;mesh.tangents=tangents;mesh.ClearBlendShapes();
        for(int frame=0;frame<original.GetBlendShapeFrameCount(0);frame++) {
            var dv=new Vector3[v.Length];var dn=new Vector3[v.Length];var dt=new Vector3[v.Length];
            original.GetBlendShapeFrameVertices(0,frame,dv,dn,dt);
            for(int i=0;i<v.Length;i++) {dv[i]=new Vector3(-dv[i].x,dv[i].y,-dv[i].z);dn[i]=new Vector3(-dn[i].x,dn[i].y,-dn[i].z);dt[i]=new Vector3(-dt[i].x,dt[i].y,-dt[i].z);}
            mesh.AddBlendShapeFrame("Length(1-10)",original.GetBlendShapeFrameWeight(0,frame),dv,dn,dt);
        }
        mesh.RecalculateBounds();
        string meshPath=Root+"/model/WireLength.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,meshPath);
        var go=new GameObject("Wire");
        try {
            var part=go.AddUdonSharpComponent<BreadboardWirePart>();
            var model=new GameObject("Model");model.transform.SetParent(go.transform,false);
            var renderer=model.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;
            renderer.localBounds=new Bounds(new Vector3(.05f,-.009f,0),new Vector3(.104f,.036f,.006f));
            part.wireRenderer=renderer;part.metalSlot=metal;part.colorSlot=color;
            part.metalMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/ResistorMetal.mat");
            string[] colors={"Brown","Red","Orange","Yellow","Green","Blue","Violet","Gray","White"};
            part.lengthMaterials=new Material[9];for(int i=0;i<9;i++)part.lengthMaterials[i]=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Resistor"+colors[i]+".mat");
            var materials=new Material[2];materials[metal]=part.metalMaterial;materials[color]=part.lengthMaterials[0];renderer.sharedMaterials=materials;
            var lineGo=new GameObject("LegacyLine");lineGo.transform.SetParent(go.transform,false);
            part.legacyLine=lineGo.AddComponent<LineRenderer>();part.legacyLine.useWorldSpace=false;part.legacyLine.widthMultiplier=.0015f;part.legacyLine.numCornerVertices=2;part.legacyLine.numCapVertices=2;lineGo.SetActive(false);
            var textGo=new GameObject("LegacyLabel");textGo.transform.SetParent(go.transform,false);
            part.legacyLabel=textGo.AddComponent<TextMeshPro>();part.labelTransform=textGo.transform;part.legacyLabel.font=TMP_Settings.defaultFontAsset;part.legacyLabel.fontSize=.05f;part.legacyLabel.alignment=TextAlignmentOptions.Center;part.legacyLabel.rectTransform.sizeDelta=new Vector2(.1f,.03f);textGo.SetActive(false);
            go.SetActive(false);UdonSharpEditorUtility.CopyProxyToUdon(part,ProxySerializationPolicy.All);
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Parts/Wire.prefab");
        } finally {Object.DestroyImmediate(go);}
        AssetDatabase.SaveAssets();
    }
}
