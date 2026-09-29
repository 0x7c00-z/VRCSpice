using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using TMPro;
using System.IO;

public static class BreadboardResistorBuilder
{
    const string Root="Assets/BreadBoard";
    static Material Mat(string name,Color color)
    {
        string path=Root+"/Materials/Resistor"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) {m=new Material(Shader.Find("Breadboard/Color"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;EditorUtility.SetDirty(m);return m;
    }
    [MenuItem("Tools/Breadboard/Install modeled resistor")]
    public static void Install()
    {
        if(Application.isPlaying) throw new System.InvalidOperationException("Exit Play first.");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/model/Breadboard.fbx").transform.Find("Resistor");
        if(!source) throw new System.Exception("FBX Resistor missing");
        var sourceMesh=source.GetComponent<MeshFilter>().sharedMesh;
        var sourceMats=source.GetComponent<MeshRenderer>().sharedMaterials;
        int[] bandSlots={-1,-1,-1,-1};int body=-1,metal=-1;
        for(int i=0;i<sourceMats.Length;i++) {
            string n=sourceMats[i].name;
            if(n=="Body")body=i; if(n=="Metal")metal=i;
            for(int j=0;j<4;j++) if(n=="color"+j)bandSlots[j]=i;
        }
        if(body<0 || metal<0 || sourceMesh.subMeshCount!=6)throw new System.Exception("Unexpected resistor material layout");
        for(int j=0;j<4;j++)if(bandSlots[j]<0)throw new System.Exception("Missing color band "+j);
        // Imported mesh: axial direction +Y; leads descend along -X.
        // Bake to +X along the board, +Y above it, and 3-pitch spacing.
        // Preserve source height: the leads extend into the board (tip plane X=-.025).
        var mesh=Object.Instantiate(sourceMesh);mesh.name="Resistor3Pitch";
        var vertices=mesh.vertices;var normals=mesh.normals;
        for(int i=0;i<vertices.Length;i++) {
            Vector3 v=vertices[i];vertices[i]=new Vector3((v.y-.005f)*.75f,v.x,-v.z-.005f);
            if(normals.Length==vertices.Length) {var n=normals[i];normals[i]=new Vector3(n.y/.75f,n.x,-n.z).normalized;}
        }
        mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();
        string meshPath=Root+"/model/Resistor3Pitch.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing) {EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;} else AssetDatabase.CreateAsset(mesh,meshPath);
        var go=new GameObject("R");
        try {
            var part=go.AddUdonSharpComponent<BreadboardResistorPart>();
            var model=new GameObject("Resistor");model.transform.SetParent(go.transform,false);
            model.AddComponent<MeshFilter>().sharedMesh=mesh;
            part.modelRoot=model.transform;part.resistorRenderer=model.AddComponent<MeshRenderer>();
            part.bodyMaterial=Mat("Body",new Color(.8f,.525f,.315f));part.metalMaterial=Mat("Metal",new Color(.65f,.68f,.7f));
            part.bodySlot=body;part.metalSlot=metal;part.bandSlots=bandSlots;
            Color[] colors={Color.black,new Color(.3f,.12f,.035f),new Color(.85f,.015f,.01f),new Color(1,.3f,0),Color.yellow,new Color(0,.5f,.12f),new Color(.03f,.16f,.9f),new Color(.55f,.08f,.7f),Color.gray,Color.white,new Color(.65f,.46f,.08f),new Color(.7f,.72f,.75f)};
            string[] names={"Black","Brown","Red","Orange","Yellow","Green","Blue","Violet","Gray","White","Gold","Silver"};
            part.codeMaterials=new Material[12];for(int i=0;i<12;i++)part.codeMaterials[i]=Mat(names[i],colors[i]);
            Material[] mats=new Material[6];for(int i=0;i<6;i++)mats[i]=part.bodyMaterial;mats[metal]=part.metalMaterial;
            mats[bandSlots[0]]=part.codeMaterials[1];mats[bandSlots[1]]=part.codeMaterials[0];mats[bandSlots[2]]=part.codeMaterials[2];mats[bandSlots[3]]=part.codeMaterials[10];
            part.resistorRenderer.sharedMaterials=mats;
            var text=new GameObject("Resistance");text.transform.SetParent(go.transform,false);
            var label=text.AddComponent<TextMeshPro>();label.font=TMP_Settings.defaultFontAsset;label.fontSize=.05f;
            label.alignment=TextAlignmentOptions.Center;label.enableWordWrapping=false;label.rectTransform.sizeDelta=new Vector2(.11f,.025f);
            part.fallbackLabel=label;part.labelTransform=text.transform;text.SetActive(false);
            go.SetActive(false);UdonSharpEditorUtility.CopyProxyToUdon(part,ProxySerializationPolicy.All);
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/Parts/R.prefab");
        } finally {Object.DestroyImmediate(go);}
        AssetDatabase.SaveAssets();
    }
    public static void RenderExamples()
    {
        var preview = new PreviewRenderUtility();
        var setup = new GameObject("render layout");
        try {
            var layout=setup.AddUdonSharpComponent<BreadboardLayout>();BreadboardBuilder.ConfigureLayout(layout);layout.holePositions[0]=Vector3.zero;
            var cat=setup.AddUdonSharpComponent<BreadboardCatalog>();var place=setup.AddUdonSharpComponent<BreadboardPlacement>();place.layout=layout;place.catalog=cat;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Parts/R.prefab");
            float[] values={1000,4700,1e12f};
            for(int i=0;i<3;i++) {
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);go.transform.position=new Vector3(0,0,i*.055f);
                var part=go.GetComponent<BreadboardResistorPart>();part.InitializePart(layout,cat,place,i+1,false,null,null);part.ApplyState(1,0,0,0,values[i],-1,true);go.SetActive(true);part.fallbackLabel.ForceMeshUpdate(true);
            }
            preview.camera.transform.position=new Vector3(.075f,.15f,.19f);preview.camera.transform.LookAt(new Vector3(.015f,.015f,.055f));
            preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=2;preview.camera.orthographic=true;preview.camera.orthographicSize=.085f;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.17f);
            preview.lights[0].intensity=1;preview.lights[1].intensity=.5f;preview.ambientColor=Color.gray;
            preview.BeginStaticPreview(new Rect(0,0,1000,1000));preview.Render();var image=preview.EndStaticPreview();
            Directory.CreateDirectory("Temp/ResistorUpgrade");File.WriteAllBytes("Temp/ResistorUpgrade/examples.png",image.EncodeToPNG());Object.DestroyImmediate(image);
        } finally {preview.Cleanup();Object.DestroyImmediate(setup);}
    }

    [MenuItem("Tools/Breadboard/Verify resistor colors")]
    public static void VerifyMenu() { Verify(); }
    public static int Verify()
    {
        int checks=0;var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Parts/R.prefab");
        var go=Object.Instantiate(prefab);
        try {
            var p=go.GetComponent<BreadboardResistorPart>();
            for(int e=-2;e<=9;e++)for(int digits=10;digits<=99;digits++) {
                float v=digits*Mathf.Pow(10,e);
                if(!p.TryColorCode(v) || p.firstDigit!=digits/10 || p.secondDigit!=digits%10 || p.multiplier!=e)throw new System.Exception("Color encode "+v);
                checks++;
            }
            foreach(float v in new[]{1e-12f,.099f,99.1e9f,1e12f,1234f,1000.1f,0f,-1f,float.NaN,float.PositiveInfinity}) {if(p.TryColorCode(v))throw new System.Exception("Expected text fallback "+v);checks++;}
            var setup=new GameObject("resistor test layout");
            try {
                var layout=setup.AddUdonSharpComponent<BreadboardLayout>();BreadboardBuilder.ConfigureLayout(layout);
                var cat=setup.AddUdonSharpComponent<BreadboardCatalog>();var place=setup.AddUdonSharpComponent<BreadboardPlacement>();place.layout=layout;place.catalog=cat;
                var valid=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/PreviewValid.mat");var invalid=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/PreviewInvalid.mat");
                p.InitializePart(layout,cat,place,1,false,valid,invalid);
                p.ApplyState(1,0,2,0,1000,-1,true);
                if(p.fallbackLabel.gameObject.activeSelf || p.resistorRenderer.sharedMaterials[p.bandSlots[2]]!=p.codeMaterials[2])throw new System.Exception("1k display");checks++;
                p.ApplyState(1,0,2,0,1234,-1,true);
                if(!p.fallbackLabel.gameObject.activeSelf || p.fallbackLabel.text!="1234 Ohm" || p.resistorRenderer.sharedMaterials[p.bandSlots[0]]!=p.bodyMaterial)throw new System.Exception("Text fallback");checks++;
                p.ApplyState(1,0,2,0,4700,-1,true);
                if(p.fallbackLabel.gameObject.activeSelf || p.resistorRenderer.sharedMaterials[p.bandSlots[0]]!=p.codeMaterials[4])throw new System.Exception("Return to code");checks++;
                p.isPreview=true;p.ApplyState(1,0,2,0,4700,-1,false);
                foreach(var m in p.resistorRenderer.sharedMaterials) {if(m!=invalid)throw new System.Exception("Preview material");checks++;}
            } finally {Object.DestroyImmediate(setup);}
        } finally {Object.DestroyImmediate(go);}
        Debug.Log("Resistor verification passed: "+checks);return checks;
    }
}
