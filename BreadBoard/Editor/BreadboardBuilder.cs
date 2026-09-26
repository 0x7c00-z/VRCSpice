using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharp;
using UdonSharpEditor;
using TMPro;
using VRC.SDK3.Components;

public static class BreadboardBuilder
{
    const string Root = "Assets/BreadBoard";
    static GameObject Child(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go;
    }
    static Material Material(string name, Color color, bool transparent = false)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find(transparent ? "Breadboard/Preview" : "Breadboard/Color")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; EditorUtility.SetDirty(mat); return mat;
    }
    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type); var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(go); return mesh;
    }
    static MeshRenderer Shape(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = Child(name, parent); go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat;
        go.transform.localPosition = pos; go.transform.localScale = scale;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return renderer;
    }
    static TMP_Text Label(string name, Transform parent, Vector3 pos, string text, float size, Vector2 rect)
    {
        var go = Child(name, parent); var label = go.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset; label.fontSize = size * .1f; label.text = text;
        label.alignment = TextAlignmentOptions.Center; label.enableWordWrapping = false;
        label.rectTransform.sizeDelta = rect; label.transform.localPosition = pos;
        label.transform.localRotation = Quaternion.Euler(90, 0, 0); return label;
    }
    public static void ConfigureLayout(BreadboardLayout layout)
    {
        layout.holeIds = new string[400]; layout.holePositions = new Vector3[400]; layout.conductorGroups = new int[400];
        for (int letter = 0; letter < 10; letter++) for (int row = 0; row < 30; row++)
        {
            int i = letter * 30 + row;
            layout.holeIds[i] = ((char)('A' + letter)) + ":" + (row + 1);
            layout.holePositions[i] = new Vector3(-.015f - row * .01f, 0, -.055f - letter * .01f - (letter >= 5 ? .02f : 0));
            layout.conductorGroups[i] = row + (letter >= 5 ? 30 : 0);
        }
        float[] zs = { -.015f, -.025f, -.195f, -.205f };
        string[] names = { "P1+:", "P1-:", "P2+:", "P2-:" };
        for (int lane = 0; lane < 4; lane++) for (int n = 0; n < 25; n++)
        {
            int i = 300 + lane * 25 + n;
            layout.holeIds[i] = names[lane] + (n + 1);
            layout.holePositions[i] = new Vector3(-.025f - (n / 5 * 6 + n % 5) * .01f, 0, zs[lane]);
            layout.conductorGroups[i] = 60 + lane % 2;
        }
        layout.positiveHole = 300; layout.groundHole = 325;
    }

    [MenuItem("Tools/Breadboard/Install in current scene")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        var board = GameObject.Find("Breadboard");
        if (!board || !board.GetComponent<VRCPickup>()) throw new System.InvalidOperationException("Expected Breadboard with existing VRCPickup.");
        if (board.transform.Find("InteractiveCircuit")) throw new System.InvalidOperationException("Already installed; edit the existing modules in Inspector.");
        if (PrefabUtility.IsPartOfPrefabInstance(board)) PrefabUtility.UnpackPrefabInstance(board, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(board);
        foreach (var udon in board.GetComponents<VRC.Udon.UdonBehaviour>()) if (udon.programSource == null) Object.DestroyImmediate(udon);
        foreach (var old in board.GetComponentsInChildren<Board>(true))
        {
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(old);
            Object.DestroyImmediate(old); if (backing) Object.DestroyImmediate(backing);
        }
        System.IO.Directory.CreateDirectory(Root + "/Materials"); System.IO.Directory.CreateDirectory(Root + "/Prefabs"); AssetDatabase.Refresh();
        var runtime = Child("InteractiveCircuit", board.transform);
        var layout = runtime.AddUdonSharpComponent<BreadboardLayout>(); ConfigureLayout(layout);
        var catalog = runtime.AddUdonSharpComponent<BreadboardCatalog>();
        var placement = runtime.AddUdonSharpComponent<BreadboardPlacement>(); placement.layout = layout; placement.catalog = catalog;
        var state = runtime.AddUdonSharpComponent<BreadboardState>(); state.layout = layout; state.catalog = catalog; state.placement = placement;
        var codec = runtime.AddUdonSharpComponent<BreadboardCodec>(); codec.state = state; codec.layout = layout; codec.catalog = catalog; codec.placement = placement;
        var connectivity = runtime.AddUdonSharpComponent<BreadboardConnectivity>(); connectivity.state = state; connectivity.layout = layout;
        var adapter = runtime.AddUdonSharpComponent<BreadboardMnaAdapter>(); adapter.state = state; adapter.layout = layout; adapter.catalog = catalog; adapter.connectivity = connectivity;
        adapter.generator = Object.FindObjectOfType<MNAGen>();
        var renderer = runtime.AddUdonSharpComponent<BreadboardRenderer>(); renderer.state = state; renderer.layout = layout; renderer.catalog = catalog; renderer.placement = placement;
        var palette = runtime.AddUdonSharpComponent<BreadboardPalette>(); palette.catalog = catalog; palette.layout = layout;
        var controller = runtime.AddUdonSharpComponent<BreadboardController>();
        var sync = Child("CircuitSync", runtime.transform).AddUdonSharpComponent<BreadboardSync>();
        controller.boardSpace = board.transform; controller.pickup = board.GetComponent<VRCPickup>(); controller.state = state;
        controller.layout = layout; controller.catalog = catalog; controller.codec = codec; controller.circuitSync = sync;
        controller.boardRenderer = renderer; controller.palette = palette; controller.adapter = adapter;
        palette.controller = controller; sync.state = state; sync.codec = codec; sync.controller = controller; sync.pickup = controller.pickup;
        var cube = PrimitiveMesh(PrimitiveType.Cube); var cylinder = PrimitiveMesh(PrimitiveType.Cylinder); var sphere = PrimitiveMesh(PrimitiveType.Sphere);
        catalog.bodyMeshes = new[] { cube, cylinder, cylinder, cylinder, cylinder, cube };
        var metal = Material("Lead", new Color(.7f,.75f,.8f)); var wire = Material("Wire", new Color(.12f,.65f,.25f));
        catalog.bodyMaterials = new[] { wire, Material("Resistor",new Color(.65f,.45f,.22f)), Material("Capacitor",new Color(.1f,.3f,.8f)), Material("Inductor",new Color(.65f,.27f,.1f)), Material("Diode",new Color(.12f,.12f,.14f)), Material("Transistor",new Color(.13f,.14f,.16f)) };
        catalog.leadMaterials = new[] { wire, metal, metal, metal, metal, metal };
        renderer.previewValid = Material("PreviewValid",new Color(.2f,1,.5f,.42f),true);
        renderer.previewInvalid = Material("PreviewInvalid",new Color(1,.15f,.1f,.42f),true);
        BuildDynamicParts(renderer);
        renderer.pointer = Shape("Pointer",runtime.transform,sphere,Material("Pointer",Color.cyan),Vector3.zero,Vector3.one*.003f).transform;
        renderer.pointer.gameObject.SetActive(false);
        BuildPalette(palette);
        sync.snapshot = codec.Encode();
        var oldNet = GameObject.Find("Net"); if (oldNet) oldNet.SetActive(false);
        foreach (var behaviour in runtime.GetComponentsInChildren<UdonSharpBehaviour>(true)) { EditorUtility.SetDirty(behaviour); UdonSharpEditorUtility.CopyProxyToUdon(behaviour,ProxySerializationPolicy.All); }
        var generator = adapter.generator; adapter.generator = null; UdonSharpEditorUtility.CopyProxyToUdon(adapter,ProxySerializationPolicy.All);
        PrefabUtility.SaveAsPrefabAssetAndConnect(board, Root + "/Prefabs/InteractiveBreadboard.prefab", InteractionMode.AutomatedAction);
        adapter.generator = generator; UdonSharpEditorUtility.CopyProxyToUdon(adapter,ProxySerializationPolicy.All);
        BindProbes(board);
        PrefabUtility.RecordPrefabInstancePropertyModifications(adapter);
        PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(adapter));
        EditorSceneManager.MarkSceneDirty(board.scene); EditorSceneManager.SaveScene(board.scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = runtime; Debug.Log("Breadboard installed: 400 holes, 128 parts, fixed rail power.");
    }

    public static void BuildDynamicParts(BreadboardRenderer renderer)
    {
        string folder = Root + "/Prefabs/Parts";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Root + "/Prefabs", "Parts");
        var catalog = renderer.catalog;
        var registered = catalog.partPrefabs;
        catalog.partPrefabs = new GameObject[catalog.kinds.Length];
        for (int kind = 0; kind < catalog.kinds.Length; kind++)
        {
            if (registered != null && kind < registered.Length && registered[kind] != null)
            { catalog.partPrefabs[kind] = registered[kind]; continue; }
            string path = folder + "/" + catalog.kinds[kind] + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing) { catalog.partPrefabs[kind] = existing; continue; }
            var go = new GameObject(catalog.kinds[kind]);
            try
            {
                var part = go.AddUdonSharpComponent<BreadboardSimplePart>();
                var body = Shape("Body",go.transform,catalog.bodyMeshes[kind],catalog.bodyMaterials[kind],Vector3.zero,Vector3.one);
                part.body = body.GetComponent<MeshFilter>(); part.bodyRenderer = body;
                part.leads = new LineRenderer[3];
                for (int p = 0; p < 3; p++)
                {
                    var line = Child("Lead" + p,go.transform).AddComponent<LineRenderer>();
                    line.useWorldSpace = false; line.sharedMaterial = catalog.leadMaterials[kind];
                    line.widthMultiplier = .0015f; line.numCornerVertices = 2; line.numCapVertices = 2;
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; part.leads[p] = line;
                }
                part.label = Label("Label",go.transform,Vector3.zero,"",.5f,new Vector2(.08f,.03f));
                part.labelTransform = part.label.transform;
                go.SetActive(false);
                UdonSharpEditorUtility.CopyProxyToUdon(part,ProxySerializationPolicy.All);
                catalog.partPrefabs[kind] = PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var old = renderer.transform.Find("Parts");
        if (old) Object.DestroyImmediate(old.gameObject);
        renderer.partsRoot = Child("Parts",renderer.transform).transform;
        UdonSharpEditorUtility.CopyProxyToUdon(catalog,ProxySerializationPolicy.All);
        UdonSharpEditorUtility.CopyProxyToUdon(renderer,ProxySerializationPolicy.All);
    }

    [MenuItem("Tools/Breadboard/Upgrade prefab to dynamic parts")]
    public static void UpgradeDynamicParts()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        string path = Root + "/Prefabs/InteractiveBreadboard.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            BuildDynamicParts(prefab.GetComponentInChildren<BreadboardRenderer>(true));
            PrefabUtility.SaveAsPrefabAsset(prefab,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
    }

    public static void BuildPalette(BreadboardPalette palette)
    {
        var runtime = palette.gameObject;
        BuildProbes(palette.controller);
        var old = runtime.transform.Find("Palette"); if (old) Object.DestroyImmediate(old.gameObject);
        var oldSupply = runtime.transform.Find("Supply"); if (oldSupply) Object.DestroyImmediate(oldSupply.gameObject);
        foreach (var legacy in runtime.GetComponents<UdonSharpBehaviour>())
        {
            if (legacy.GetType().Name != "BreadboardNumberEntry") continue;
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(legacy);
            Object.DestroyImmediate(legacy); if (backing) Object.DestroyImmediate(backing);
        }
        var cube = PrimitiveMesh(PrimitiveType.Cube);
        var ui = Child("Palette",runtime.transform);
        Shape("Panel",ui.transform,cube,Material("Panel",new Color(.035f,.05f,.07f)),new Vector3(-.16f,0,.105f),new Vector3(.32f,.003f,.175f));
        palette.normalMaterial = Material("Button",new Color(.1f,.15f,.22f)); palette.selectedMaterial = Material("Selected",new Color(.1f,.38f,.5f)); palette.hoverMaterial = Material("Hover",new Color(.25f,.55f,.6f));
        palette.buttons = new Renderer[24]; palette.buttonLabels = new TMP_Text[24]; palette.buttonCenters = new Vector3[24];
        for (int i = 0; i < 24; i++)
        {
            var pos = new Vector3(-.285f + i%6*.05f,.003f,.135f - i/6*.031f);
            palette.buttonCenters[i] = pos;
            palette.buttons[i] = Shape("Button"+i,ui.transform,cube,palette.normalMaterial,pos,new Vector3(.046f,.003f,.026f));
            palette.buttonLabels[i] = Label("Caption"+i,ui.transform,pos+Vector3.up*.003f,"",.65f,new Vector2(.045f,.025f));
            palette.buttonLabels[i].enableAutoSizing = true;
            palette.buttonLabels[i].fontSizeMin = .032f; palette.buttonLabels[i].fontSizeMax = .10f;
        }
        palette.selectionLabel = Label("Selection",ui.transform,new Vector3(-.16f,.004f,.162f),"",.85f,new Vector2(.31f,.013f));
        palette.statusLabel = Label("Status",ui.transform,new Vector3(-.16f,.004f,.18f),"Hold board to edit",.65f,new Vector2(.31f,.013f));
        palette.supplyLabel = Label("Supply",runtime.transform,new Vector3(-.16f,.004f,.009f),"",.65f,new Vector2(.31f,.013f));
        palette.controller.state.Initialize(); palette.page = 0; palette.Refresh();
    }

    [MenuItem("Tools/Breadboard/Upgrade prefab UI")]
    public static void UpgradePalette()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
        var root = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/InteractiveBreadboard.prefab");
        try
        {
            var palette = root.GetComponentInChildren<BreadboardPalette>(); BuildPalette(palette);
            var sync = root.GetComponentInChildren<BreadboardSync>(); sync.snapshot = sync.codec.Encode();
            foreach (var behaviour in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
            { EditorUtility.SetDirty(behaviour); UdonSharpEditorUtility.CopyProxyToUdon(behaviour,ProxySerializationPolicy.All); }
            PrefabUtility.SaveAsPrefabAsset(root,Root + "/Prefabs/InteractiveBreadboard.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var sceneBoard = GameObject.Find("Breadboard");
        if (sceneBoard) { BindProbes(sceneBoard); EditorSceneManager.MarkSceneDirty(sceneBoard.scene); EditorSceneManager.SaveScene(sceneBoard.scene); }
        AssetDatabase.SaveAssets();
    }

    static void BuildProbes(BreadboardController controller)
    {
        var go = controller.gameObject;
        var probes = go.GetComponent<BreadboardProbes>();
        if (!probes) probes = go.AddUdonSharpComponent<BreadboardProbes>();
        controller.probes = probes; controller.adapter.probes = probes;
        probes.layout = controller.layout; probes.state = controller.state;
        probes.connectivity = controller.adapter.connectivity; probes.adapter = controller.adapter;
        var mesh = PrimitiveMesh(PrimitiveType.Sphere);
        for (int channel = 0; channel < 2; channel++)
        {
            if ((channel == 0 ? probes.marker1 : probes.marker2) != null) continue;
            var marker = Child("ProbeCH"+(channel+1),go.transform);
            var mat = Material("ProbeCH"+(channel+1),channel == 0 ? Color.yellow : Color.cyan);
            Shape("Tip",marker.transform,mesh,mat,Vector3.zero,Vector3.one*.006f);
            var label = Label("Label",marker.transform,Vector3.up*.007f,"CH"+(channel+1),.7f,new Vector2(.03f,.013f));
            label.color = channel == 0 ? Color.yellow : Color.cyan;
            if (channel == 0) probes.marker1 = marker.transform; else probes.marker2 = marker.transform;
            marker.SetActive(false);
        }
    }

    public static void BindProbes(GameObject board)
    {
        var probes = board.GetComponentInChildren<BreadboardProbes>();
        var adapter = board.GetComponentInChildren<BreadboardMnaAdapter>();
        if (!probes || !adapter || !adapter.generator) return;
        var test = adapter.generator.GetComponent<Test>();
        if (!test) return;
        var serialized = new SerializedObject(test);
        probes.channel1Input = serialized.FindProperty("probefield1").objectReferenceValue as TMP_InputField;
        probes.channel2Input = serialized.FindProperty("probefield2").objectReferenceValue as TMP_InputField;
        var controller = board.GetComponentInChildren<BreadboardController>();
        adapter.probes = probes; controller.probes = probes;
        foreach (var behaviour in new UdonSharpBehaviour[] { probes,adapter,controller })
        {
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour,ProxySerializationPolicy.All); EditorUtility.SetDirty(behaviour);
            if (PrefabUtility.IsPartOfPrefabInstance(behaviour))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
                PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));
            }
        }
    }
}
