using System.IO;
using TMPro;
using TMPro.EditorUtilities;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class OscilloscopeDumpBuilder
{
    private const string ScenePath = "Assets/VRCSpice/Scene.unity";
    private const string ControllerScript = "Assets/VRCSpice/Oscilloscope/OscilloscopeController.cs";
    private const string ControllerAsset = "Assets/VRCSpice/Oscilloscope/OscilloscopeController.asset";
    private const string TriggerSyncScript = "Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerSettingsSync.cs";
    private const string TriggerSyncAsset = "Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerSettingsSync.asset";
    private const string ExtractMaterialPath = "Assets/VRCSpice/MNATools/Shader/SolverOutputDump.mat";
    private const string StateMaterialPath = "Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerState.mat";
    private const string PreMaterialPath = "Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerPre.mat";
    private const string CaptureMaterialPath = "Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerCapture.mat";
    private const string CopyMaterialPath = "Assets/VRCSpice/Oscilloscope/OscilloscopeCopy.mat";
    private const string PointMeshPath = "Assets/VRCSpice/Oscilloscope/OscilloscopePoint.asset";

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new System.Exception("Oscilloscope dump check failed: " + message);
    }

    private static void EnsureProgramAsset(string scriptPath, string assetPath, string label)
    {
        UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath);
        if (program != null) return;
        MonoScript source = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
        Check(source != null, label + " script imported");
        program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        program.sourceCsScript = source;
        AssetDatabase.CreateAsset(program, assetPath);
    }

    private static void EnsureProgram()
    {
        EnsureProgramAsset(ControllerScript, ControllerAsset, "controller");
        EnsureProgramAsset(TriggerSyncScript, TriggerSyncAsset, "trigger settings sync");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
    }

    private static Material EnsureMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        Check(shader != null, "find " + shaderName);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh EnsurePointMesh()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(PointMeshPath);
        if (mesh == null)
        {
            mesh = new Mesh(); mesh.name = "OscilloscopePoint";
            AssetDatabase.CreateAsset(mesh, PointMeshPath);
        }
        mesh.Clear();
        mesh.vertices = new[] { Vector3.zero, Vector3.right };
        mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Points, 0);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static TMP_DefaultControls.Resources StandardResources()
    {
        TMP_DefaultControls.Resources r = new TMP_DefaultControls.Resources();
        r.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        r.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        r.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        r.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        r.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        r.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        r.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
        return r;
    }

    private static DefaultControls.Resources ScrollbarResources()
    {
        DefaultControls.Resources r = new DefaultControls.Resources();
        r.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        r.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        r.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        return r;
    }

    private static void Parent(GameObject child, Transform parent, Vector2 position, Vector2 size)
    {
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        child.layer = parent.gameObject.layer;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 position,
        Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = TMP_DefaultControls.CreateText(StandardResources());
        go.name = name; Parent(go, parent, position, size);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = alignment; text.raycastTarget = false;
        return text;
    }

    private static Scrollbar ScrollbarControl(string name, Transform parent, Vector2 position,
        Vector2 size, int steps, float value)
    {
        GameObject go = DefaultControls.CreateScrollbar(ScrollbarResources());
        go.name = name; Parent(go, parent, position, size);
        Scrollbar scrollbar = go.GetComponent<Scrollbar>();
        scrollbar.numberOfSteps = steps; scrollbar.value = value;
        return scrollbar;
    }

    private static TMP_InputField InputNearest(Transform root, float y)
    {
        TMP_InputField[] fields = root.GetComponentsInChildren<TMP_InputField>(true);
        TMP_InputField best = null; float distance = float.MaxValue;
        foreach (TMP_InputField field in fields)
        {
            RectTransform rect = field.transform as RectTransform;
            if (rect == null) continue;
            float next = Mathf.Abs(rect.anchoredPosition.y - y);
            if (next < distance) { distance = next; best = field; }
        }
        return distance < 20f ? best : null;
    }

    private static TMP_InputField DirectInput(Transform root, string name)
    {
        Transform child = root.Find(name);
        return child == null ? null : child.GetComponent<TMP_InputField>();
    }

    private static void CopyProxy(UdonSharpBehaviour behaviour)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.All);
        EditorUtility.SetDirty(behaviour);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));
    }

    private static void AddChangeEvent(UnityEventBase unityEvent, VRC.Udon.UdonBehaviour backing,
        string eventName)
    {
        for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (unityEvent.GetPersistentTarget(i) == backing &&
                unityEvent.GetPersistentMethodName(i) == "SendCustomEvent")
                UnityEventTools.RemovePersistentListener(unityEvent, i);
        }
        UnityEventTools.AddStringPersistentListener(unityEvent, backing.SendCustomEvent, eventName);
    }

    private static GameObject Waveform(Transform screen, string name, Mesh mesh, Material material)
    {
        Transform old = screen.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(screen, false); go.transform.localPosition = new Vector3(0f, 0f, -0.012f);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return go;
    }

    private static void BuildTriggerPanel(Transform ui, OscilloscopeController controller,
        VRC.Udon.UdonBehaviour backing)
    {
        Transform old = ui.Find("OscilloscopeTriggerPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RectTransform uiRect = ui as RectTransform;
        if (uiRect != null && uiRect.sizeDelta.y < 570f)
            uiRect.sizeDelta = new Vector2(Mathf.Max(400f, uiRect.sizeDelta.x), 570f);

        GameObject panel = new GameObject("OscilloscopeTriggerPanel", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        Parent(panel, ui, new Vector2(0f, -275f), new Vector2(380f, 250f));
        panel.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.07f, 0.94f);
        Text("TriggerTitle", panel.transform, "TRIGGER", new Vector2(0f, 105f),
            new Vector2(350f, 28f), 21, TextAlignmentOptions.Center);

        Text("EnableLabel", panel.transform, "ENABLE", new Vector2(-145f, 66f),
            new Vector2(80f, 28f), 16, TextAlignmentOptions.MidlineLeft);
        Scrollbar enable = ScrollbarControl("TriggerEnableScrollbar", panel.transform,
            new Vector2(-5f, 66f), new Vector2(150f, 20f), 2, 0f);
        TMP_Text enableText = Text("TriggerEnableText", panel.transform, "OFF", new Vector2(120f, 66f),
            new Vector2(70f, 28f), 16, TextAlignmentOptions.Center);

        Text("ModeLabel", panel.transform, "MODE", new Vector2(-145f, 26f),
            new Vector2(80f, 28f), 16, TextAlignmentOptions.MidlineLeft);
        Scrollbar mode = ScrollbarControl("TriggerModeScrollbar", panel.transform,
            new Vector2(-5f, 26f), new Vector2(150f, 20f), 3, 0f);
        TMP_Text modeText = Text("TriggerModeText", panel.transform, "SINGLE", new Vector2(120f, 26f),
            new Vector2(80f, 28f), 15, TextAlignmentOptions.Center);

        Text("SourceLabel", panel.transform, "SOURCE", new Vector2(-145f, -14f),
            new Vector2(80f, 28f), 16, TextAlignmentOptions.MidlineLeft);
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(StandardResources());
        dropdownObject.name = "TriggerSourceDropdown";
        Parent(dropdownObject, panel.transform, new Vector2(35f, -14f), new Vector2(230f, 32f));
        TMP_Dropdown source = dropdownObject.GetComponent<TMP_Dropdown>();
        source.ClearOptions(); source.options.Add(new TMP_Dropdown.OptionData("CH1"));
        source.options.Add(new TMP_Dropdown.OptionData("CH2")); source.value = 0;

        Text("EdgeLabel", panel.transform, "EDGE", new Vector2(-145f, -54f),
            new Vector2(80f, 28f), 16, TextAlignmentOptions.MidlineLeft);
        Scrollbar edge = ScrollbarControl("TriggerEdgeScrollbar", panel.transform,
            new Vector2(-5f, -54f), new Vector2(150f, 20f), 2, 0f);
        TMP_Text edgeText = Text("TriggerEdgeText", panel.transform, "RISE", new Vector2(120f, -54f),
            new Vector2(70f, 28f), 16, TextAlignmentOptions.Center);

        Text("LevelLabel", panel.transform, "LEVEL", new Vector2(-145f, -94f),
            new Vector2(80f, 28f), 16, TextAlignmentOptions.MidlineLeft);
        GameObject levelObject = TMP_DefaultControls.CreateInputField(StandardResources());
        levelObject.name = "TriggerLevelInput";
        Parent(levelObject, panel.transform, new Vector2(5f, -94f), new Vector2(170f, 32f));
        TMP_InputField level = levelObject.GetComponent<TMP_InputField>();
        level.contentType = TMP_InputField.ContentType.DecimalNumber;
        level.lineType = TMP_InputField.LineType.SingleLine; level.text = "0";
        Text("LevelUnit", panel.transform, "V", new Vector2(112f, -94f),
            new Vector2(35f, 28f), 16, TextAlignmentOptions.Center);

        controller.triggerEnableScrollbar = enable;
        controller.triggerModeScrollbar = mode;
        controller.triggerSourceDropdown = source;
        controller.triggerEdgeScrollbar = edge;
        controller.triggerLevelField = level;
        controller.triggerEnableText = enableText;
        controller.triggerModeText = modeText;
        controller.triggerEdgeText = edgeText;
        CopyProxy(controller);

        string eventName = nameof(OscilloscopeController.ChangeTriggerSettings);
        AddChangeEvent(enable.onValueChanged, backing, eventName);
        AddChangeEvent(mode.onValueChanged, backing, eventName);
        AddChangeEvent(source.onValueChanged, backing, eventName);
        AddChangeEvent(edge.onValueChanged, backing, eventName);
        AddChangeEvent(level.onSubmit, backing, eventName);
        AddChangeEvent(level.onEndEdit, backing, eventName);
        Check(enable.onValueChanged.GetPersistentEventCount() > 0 &&
            mode.onValueChanged.GetPersistentEventCount() > 0 &&
            source.onValueChanged.GetPersistentEventCount() > 0 &&
            edge.onValueChanged.GetPersistentEventCount() > 0 &&
            level.onSubmit.GetPersistentEventCount() > 0 &&
            level.onEndEdit.GetPersistentEventCount() > 0, "trigger UI events connected");
    }

    private static OscilloscopeTriggerSettingsSync EnsureTriggerSettingsSync(
        GameObject oscillo, OscilloscopeController controller)
    {
        Transform triggerSyncTransform = oscillo.transform.Find("TriggerSettingsSync");
        if (triggerSyncTransform == null)
        {
            GameObject triggerSyncObject = new GameObject("TriggerSettingsSync");
            triggerSyncObject.transform.SetParent(oscillo.transform, false);
            triggerSyncTransform = triggerSyncObject.transform;
        }
        OscilloscopeTriggerSettingsSync triggerSettingsSync =
            triggerSyncTransform.GetComponent<OscilloscopeTriggerSettingsSync>();
        if (triggerSettingsSync == null)
            triggerSettingsSync = triggerSyncTransform.gameObject.AddUdonSharpComponent<OscilloscopeTriggerSettingsSync>();
        controller.triggerSettingsSync = triggerSettingsSync;
        triggerSettingsSync.controller = controller;
        triggerSettingsSync.timeDivisionIndex = 2;
        triggerSettingsSync.ch1VoltageDivision = 5f;
        triggerSettingsSync.ch2VoltageDivision = 5f;
        VRC.Udon.UdonBehaviour triggerSyncBacking =
            UdonSharpEditorUtility.GetBackingUdonBehaviour(triggerSettingsSync);
        triggerSyncBacking.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual;
        EditorUtility.SetDirty(triggerSyncBacking);
        CopyProxy(triggerSettingsSync);
        CopyProxy(controller);
        return triggerSettingsSync;
    }

    [MenuItem("Tools/VRCSpice/Install shared oscilloscope trigger settings")]
    public static void InstallTriggerSettingsSyncCurrentWorld()
    {
        EnsureProgram();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject oscillo = GameObject.Find("Oscillo"); Check(oscillo != null, "Oscillo root");
        OscilloscopeController controller = oscillo.GetComponent<OscilloscopeController>();
        Check(controller != null, "OscilloscopeController");
        OscilloscopeTriggerSettingsSync triggerSettingsSync = EnsureTriggerSettingsSync(oscillo, controller);
        Check(controller.triggerSettingsSync == triggerSettingsSync &&
            triggerSettingsSync.controller == controller, "shared trigger settings references");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed shared oscilloscope trigger settings without rebuilding the oscilloscope UI.");
    }

    [MenuItem("Tools/VRCSpice/Install oscilloscope output dump")]
    public static void BuildCurrentWorld()
    {
        EnsureProgram();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject oscillo = GameObject.Find("Oscillo"); Check(oscillo != null, "Oscillo root");
        Transform body = oscillo.transform.Find("Cube"); Check(body != null, "Oscillo/Cube");
        Transform solverTransform = body.Find("Solver");
        Transform ui = body.Find("OscUI");
        Transform screen = body.Find("Screen");
        Check(solverTransform != null && ui != null && screen != null, "Solver, OscUI and Screen hierarchy");
        MNASolve solver = solverTransform.GetComponent<MNASolve>(); Check(solver != null, "MNASolve");
        OscilloscopeController controller = oscillo.GetComponent<OscilloscopeController>();
        if (controller == null) controller = oscillo.AddUdonSharpComponent<OscilloscopeController>();
        EnsureTriggerSettingsSync(oscillo, controller);

        Material extractor = EnsureMaterial(ExtractMaterialPath, "VRCSpice/Solver Output Dump");
        Material state = EnsureMaterial(StateMaterialPath, "VRCSpice/Oscilloscope Trigger State");
        Material pre = EnsureMaterial(PreMaterialPath, "VRCSpice/Oscilloscope Trigger Pre");
        Material capture = EnsureMaterial(CaptureMaterialPath, "VRCSpice/Oscilloscope Trigger Capture");
        Material copy = EnsureMaterial(CopyMaterialPath, "VRCSpice/Oscilloscope Copy");
        Material ch1 = EnsureMaterial("Assets/VRCSpice/Oscilloscope/CH1LineMaterial.mat",
            "VRCSpice/Oscilloscope Waveform");
        Material ch2 = EnsureMaterial("Assets/VRCSpice/Oscilloscope/CH2LineMaterial.mat",
            "VRCSpice/Oscilloscope Waveform");
        ch1.SetInteger("_Channel", 0); ch1.SetColor("_LineColor", new Color(0.02f, 1f, 0.02f, 1f));
        ch2.SetInteger("_Channel", 1); ch2.SetColor("_LineColor", new Color(1f, 0.95f, 0.02f, 1f));

        SerializedObject solverObject = new SerializedObject(solver);
        solverObject.FindProperty("dumpExtractor").objectReferenceValue = extractor;
        SerializedProperty diagnostics = solverObject.FindProperty("outputMaterial"); diagnostics.arraySize = 2;
        diagnostics.GetArrayElementAtIndex(0).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/VRCSpice/FontAsset/font.mat");
        diagnostics.GetArrayElementAtIndex(1).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/VRCSpice/Debug.mat");
        solverObject.ApplyModifiedPropertiesWithoutUndo(); CopyProxy(solver);

        MeshRenderer oldRenderer = solverTransform.GetComponent<MeshRenderer>();
        if (oldRenderer != null) Object.DestroyImmediate(oldRenderer);
        MeshFilter oldFilter = solverTransform.GetComponent<MeshFilter>();
        if (oldFilter != null) Object.DestroyImmediate(oldFilter);
        Transform oldGrid = solverTransform.Find("Grid");
        if (oldGrid != null) Object.DestroyImmediate(oldGrid.gameObject);

        Transform previousScrollbar = ui.Find("scrollbar_divt");
        if (previousScrollbar != null) Object.DestroyImmediate(previousScrollbar.gameObject);
        Scrollbar scrollbar = ScrollbarControl("scrollbar_divt", ui, new Vector2(15f, -82f),
            new Vector2(220f, 22f), 5, 0.5f);

        TMP_Text divText = null; Transform divTransform = ui.Find("divt_x10");
        if (divTransform == null) divTransform = ui.Find("divt_display");
        if (divTransform != null) divText = divTransform.GetComponent<TMP_Text>();
        Check(divText != null, "Time/div display text"); divTransform.name = "divt_display";
        divText.text = "2 ms/div  x10"; divText.fontSize = 20;
        divText.alignment = TextAlignmentOptions.Center; divText.raycastTarget = false;
        RectTransform divRect = divTransform as RectTransform; divRect.sizeDelta = new Vector2(260f, 36f);

        TMP_InputField probe1 = controller.probeField1 != null ? controller.probeField1 : InputNearest(ui, 100f);
        TMP_InputField probe2 = controller.probeField2 != null ? controller.probeField2 : InputNearest(ui, 50f);
        TMP_InputField yScale1 = DirectInput(ui, "InputFieldCH1divy");
        if (yScale1 == null) yScale1 = controller.yScaleField;
        if (yScale1 == null) yScale1 = InputNearest(ui, -118f);
        Check(probe1 != null && probe2 != null && yScale1 != null, "probe and CH1 Y scale fields");
        yScale1.gameObject.name = "InputFieldCH1divy";

        TMP_InputField yScale2 = DirectInput(ui, "InputFieldCH2divy");
        if (yScale2 == null)
        {
            GameObject copyField = Object.Instantiate(yScale1.gameObject, ui, false);
            copyField.name = "InputFieldCH2divy";
            RectTransform copyRect = copyField.transform as RectTransform;
            RectTransform firstRect = yScale1.transform as RectTransform;
            copyRect.anchoredPosition = new Vector2(firstRect.anchoredPosition.x,
                firstRect.anchoredPosition.y - 82f);
            yScale2 = copyField.GetComponent<TMP_InputField>();
        }

        controller.solver = solver; controller.probeField1 = probe1; controller.probeField2 = probe2;
        controller.yScaleField = yScale1; controller.yScaleField2 = yScale2;
        controller.timeDivScrollbar = scrollbar; controller.timeDivText = divText;
        controller.bufferTemplate = AssetDatabase.LoadAssetAtPath<RenderTexture>(
            "Assets/VRCSpice/MNATools/Shader/SolverBufferTemplate.renderTexture");
        controller.triggerStateMaterial = state; controller.triggerPreMaterial = pre;
        controller.triggerCaptureMaterial = capture; controller.copyMaterial = copy;
        controller.ch1Material = ch1; controller.ch2Material = ch2;
        CopyProxy(controller);
        VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(controller);
        AddChangeEvent(scrollbar.onValueChanged, backing, nameof(OscilloscopeController.ChangeTimeDivision));
        AddChangeEvent(yScale1.onSubmit, backing, nameof(OscilloscopeController.ChangeVoltageScale));
        AddChangeEvent(yScale1.onEndEdit, backing, nameof(OscilloscopeController.ChangeVoltageScale));
        AddChangeEvent(yScale2.onSubmit, backing, nameof(OscilloscopeController.ChangeVoltageScale));
        AddChangeEvent(yScale2.onEndEdit, backing, nameof(OscilloscopeController.ChangeVoltageScale));
        Transform ch1Label = ui.Find("CH1divVLabel");
        Transform ch2Label = ui.Find("CH2divVLabel");
        if (ch1Label != null && ch1Label.GetComponent<TMP_Text>() != null)
            ch1Label.GetComponent<TMP_Text>().raycastTarget = false;
        if (ch2Label != null && ch2Label.GetComponent<TMP_Text>() != null)
            ch2Label.GetComponent<TMP_Text>().raycastTarget = false;
        BuildTriggerPanel(ui, controller, backing);

        Mesh point = EnsurePointMesh();
        Waveform(screen, "CH1Waveform", point, ch1);
        Waveform(screen, "CH2Waveform", point, ch2);
        EditorUtility.SetDirty(ch1); EditorUtility.SetDirty(ch2); EditorUtility.SetDirty(scrollbar);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed the HLSL 100+100 oscilloscope trigger and shared trigger settings UI.");
    }

    private static void VerifyShader(string path, params string[] properties)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path); Check(shader != null, "load " + path);
        Check(!ShaderUtil.ShaderHasError(shader), "compile " + path);
        Material material = new Material(shader);
        foreach (string property in properties) Check(material.HasProperty(property), path + " " + property);
        Object.DestroyImmediate(material);
    }

    private static int Candidates(int start, int finish, int stride)
    {
        int first = start % stride == 0 ? start : start + stride - start % stride;
        int last = finish - finish % stride; return last < first ? 0 : (last - first) / stride + 1;
    }

    private static bool Crossed(float previous, float current, float level, bool falling)
    {
        return falling ? previous > level && current <= level : previous < level && current >= level;
    }

    private static void VerifyTriggerContract()
    {
        Check(!Crossed(-1f, -0.1f, 0f, false), "rise waits below threshold");
        Check(Crossed(-1f, 0f, 0f, false), "rise includes threshold equality");
        Check(Crossed(1f, 0f, 0f, true), "fall includes threshold equality");
        Check(!Crossed(0f, 1f, 0f, false), "level residence does not retrigger");
        int autoFrames = 0;
        for (int i = 0; i < 119; i++) autoFrames++;
        Check(autoFrames == 119, "AUTO does not force at 119 frames");
        autoFrames++;
        Check(autoFrames == 120, "AUTO forces at 120 data-bearing frames");
        string common = File.ReadAllText("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerCommon.cginc");
        Check(common.Contains("OSC_PRE_CAPACITY 100u") && common.Contains("OSC_POST_CAPACITY 100u"),
            "100 point pre/post capacities");
        Check(common.Contains("OSC_AUTO_FRAMES 120u"), "120 frame AUTO timeout");
        Check(common.Contains("OSC_STATE_HAS_TRIGGER_DISPLAY"), "trigger display state slot");
        string state = File.ReadAllText("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerState.shader");
        Check(state.Contains("ready>=OSC_PRE_CAPACITY") && state.Contains("OSC_FLAG_CAPTURE_COMPLETE"),
            "arming and complete-capture gates");
        string copy = File.ReadAllText("Assets/VRCSpice/Oscilloscope/OscilloscopeCopy.shader");
        Check(copy.Contains("hasTriggerDisplay==0u") && copy.Contains("acquisition==OSC_WAITING"),
            "initial arming keeps the continuous display live");
        string waveform = File.ReadAllText("Assets/VRCSpice/Oscilloscope/OscilloscopeWaveform.shader");
        Check(waveform.Contains("clip(0.5-abs(input.localY))"),
            "waveform clips beyond four vertical divisions");
    }

    private static RenderTexture UIntBuffer(int width, int height)
    {
        RenderTexture buffer = new RenderTexture(width, height, 0, RenderTextureFormat.RInt,
            RenderTextureReadWrite.Linear);
        buffer.filterMode = FilterMode.Point; buffer.wrapMode = TextureWrapMode.Clamp;
        buffer.useMipMap = false; buffer.autoGenerateMips = false; buffer.antiAliasing = 1;
        buffer.Create(); return buffer;
    }

    private static uint ReadUInt(RenderTexture buffer, int x, int y)
    {
        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(buffer, 0);
        request.WaitForCompletion(); Check(!request.hasError, "GPU readback " + buffer.name);
        return request.GetData<uint>()[y * buffer.width + x];
    }

    private static void SetGpuTriggerProperties(Material material, RenderTexture solverDump, bool forceReset,
        int triggerEnabled, int triggerMode, int triggerRevision)
    {
        material.SetTexture("_SolverDump", solverDump);
        material.SetInteger("_Row1", 0); material.SetInteger("_Row2", 1);
        material.SetInteger("_Stride", 1); material.SetInteger("_SamplingRevision", 1);
        material.SetInteger("_TriggerRevision", triggerRevision);
        material.SetInteger("_TriggerEnabled", triggerEnabled);
        material.SetInteger("_TriggerMode", triggerMode); material.SetInteger("_TriggerSource", 0);
        material.SetInteger("_TriggerFalling", 0); material.SetFloat("_TriggerLevel", 0f);
        material.SetInteger("_ForceReset", forceReset ? 1 : 0);
    }

    private static void RunGpuTriggerFrame(int sequence, bool forceReset, RenderTexture solverDump,
        Material testDump, Material stateMaterial, Material preMaterial, Material captureMaterial,
        Material displayMaterial, ref RenderTexture stateSource, ref RenderTexture stateDestination,
        ref RenderTexture preSource, ref RenderTexture preDestination,
        ref RenderTexture captureSource, ref RenderTexture captureDestination,
        ref RenderTexture displaySource, ref RenderTexture displayDestination,
        int triggerEnabled = 1, int triggerMode = 0, int triggerRevision = 1)
    {
        testDump.SetInteger("_Sequence", sequence); testDump.SetInteger("_Count", sequence);
        testDump.SetInteger("_Generation", 1);
        Graphics.Blit(Texture2D.blackTexture, solverDump, testDump);
        SetGpuTriggerProperties(stateMaterial, solverDump, forceReset,
            triggerEnabled, triggerMode, triggerRevision);
        stateMaterial.SetTexture("_PreDump", preSource);
        Graphics.Blit(stateSource, stateDestination, stateMaterial);
        SetGpuTriggerProperties(preMaterial, solverDump, forceReset,
            triggerEnabled, triggerMode, triggerRevision);
        preMaterial.SetTexture("_StateDump", stateDestination);
        preMaterial.SetTexture("_PreviousState", stateSource);
        Graphics.Blit(preSource, preDestination, preMaterial);
        SetGpuTriggerProperties(captureMaterial, solverDump, forceReset,
            triggerEnabled, triggerMode, triggerRevision);
        captureMaterial.SetTexture("_StateDump", stateDestination);
        captureMaterial.SetTexture("_PreviousState", stateSource);
        captureMaterial.SetTexture("_PreDump", preSource);
        Graphics.Blit(captureSource, captureDestination, captureMaterial);
        SetGpuTriggerProperties(displayMaterial, solverDump, forceReset,
            triggerEnabled, triggerMode, triggerRevision);
        displayMaterial.SetTexture("_StateDump", stateDestination);
        displayMaterial.SetTexture("_CaptureDump", captureDestination);
        Graphics.Blit(displaySource, displayDestination, displayMaterial);
        RenderTexture swap = stateSource; stateSource = stateDestination; stateDestination = swap;
        swap = preSource; preSource = preDestination; preDestination = swap;
        swap = captureSource; captureSource = captureDestination; captureDestination = swap;
        swap = displaySource; displaySource = displayDestination; displayDestination = swap;
    }

    private static void VerifyGpuTriggerPipeline()
    {
        Check(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RInt), "RInt render textures");
        Check(SystemInfo.supportsAsyncGPUReadback, "asynchronous GPU readback");
        Shader testShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VRCSpice/BreadBoard/Editor/OscilloscopeTriggerTestDump.shader");
        Shader stateShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerState.shader");
        Shader preShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerPre.shader");
        Shader captureShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerCapture.shader");
        Shader displayShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/VRCSpice/Oscilloscope/OscilloscopeCopy.shader");
        Check(testShader != null && stateShader != null && preShader != null && captureShader != null && displayShader != null,
            "GPU verification shaders loaded");
        Material testDump = new Material(testShader); Material state = new Material(stateShader);
        Material pre = new Material(preShader); Material capture = new Material(captureShader); Material display = new Material(displayShader);
        RenderTexture solverDump = UIntBuffer(16, 1024);
        RenderTexture state0 = UIntBuffer(32, 1), state1 = UIntBuffer(32, 1);
        RenderTexture pre0 = UIntBuffer(4, 128), pre1 = UIntBuffer(4, 128);
        RenderTexture capture0 = UIntBuffer(4, 256), capture1 = UIntBuffer(4, 256);
        RenderTexture display0 = UIntBuffer(4, 256), display1 = UIntBuffer(4, 256);
        try
        {
            RunGpuTriggerFrame(99, true, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1, ref display0, ref display1);
            Check(ReadUInt(state0, 5, 0) == 99u && ReadUInt(state0, 4, 0) == 1u, "GPU remains PREPARING at 99 points");
            RunGpuTriggerFrame(100, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1, ref display0, ref display1);
            Check(ReadUInt(state0, 5, 0) == 100u && ReadUInt(state0, 4, 0) == 2u,
                "GPU arms after 100 points without retroactive trigger");
            Check(ReadUInt(display0, 0, 0) == 100u,
                "initial WAITING keeps the continuous waveform live");
            RunGpuTriggerFrame(101, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1, ref display0, ref display1);
            Check(ReadUInt(state0, 4, 0) == 3u && ReadUInt(state0, 9, 0) == 1u, "GPU starts capture on point 101");
            Check(ReadUInt(display0, 0, 0) == 100u, "display freezes only after the trigger starts");
            RunGpuTriggerFrame(200, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1, ref display0, ref display1);
            Check(ReadUInt(state0, 4, 0) == 4u && ReadUInt(state0, 9, 0) == 100u,
                "SINGLE enters HOLD after 100 post-trigger points");
            Check(ReadUInt(display0, 0, 0) == 200u, "completed display contains 200 points");
            Check(ReadUInt(state0, 21, 0) == 1u, "completed trigger display is remembered");
            Check(Mathf.Approximately(System.BitConverter.Int32BitsToSingle((int)ReadUInt(display0, 1, 100)), 0f),
                "trigger point is display index 100");

            RunGpuTriggerFrame(201, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 0, 0, 2);
            RunGpuTriggerFrame(202, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 0, 3);
            Check(ReadUInt(state0, 21, 0) == 0u && ReadUInt(display0, 1, 0) == 202u,
                "OFF to ON clears the completed marker and resumes the live display");

            RunGpuTriggerFrame(99, true, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 1, 4);
            RunGpuTriggerFrame(100, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 1, 4);
            RunGpuTriggerFrame(101, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 1, 4);
            RunGpuTriggerFrame(200, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 1, 4);
            Check(ReadUInt(state0, 4, 0) == 2u && ReadUInt(state0, 21, 0) == 1u,
                "NORMAL rearms while remembering its completed display");
            RunGpuTriggerFrame(201, false, solverDump, testDump, state, pre, capture, display,
                ref state0, ref state1, ref pre0, ref pre1, ref capture0, ref capture1,
                ref display0, ref display1, 1, 2, 5);
            Check(ReadUInt(state0, 21, 0) == 1u && ReadUInt(display0, 1, 0) == 200u,
                "NORMAL and AUTO waiting retain the last completed trigger waveform");
        }
        finally
        {
            Object.DestroyImmediate(testDump); Object.DestroyImmediate(state); Object.DestroyImmediate(pre);
            Object.DestroyImmediate(capture); Object.DestroyImmediate(display); Object.DestroyImmediate(solverDump);
            Object.DestroyImmediate(state0); Object.DestroyImmediate(state1); Object.DestroyImmediate(pre0); Object.DestroyImmediate(pre1);
            Object.DestroyImmediate(capture0); Object.DestroyImmediate(capture1); Object.DestroyImmediate(display0); Object.DestroyImmediate(display1);
        }
    }

    [MenuItem("Tools/VRCSpice/Build and verify oscilloscope output dump")]
    public static void BuildAndVerify()
    {
        BuildCurrentWorld();
        Check(Candidates(1, 1000, 1) == 1000, "x1 candidates");
        Check(Candidates(1, 1000, 5) == 200, "x5 candidates");
        Check(Candidates(1, 1000, 10) == 100, "x10 candidates");
        Check(Candidates(1, 1000, 50) == 20, "x50 candidates");
        Check(Candidates(1, 1000, 100) == 10, "x100 candidates");
        string variables = File.ReadAllText("Assets/VRCSpice/MNATools/Shader/Solver_variables.hlsl");
        Check(variables.Contains("OUTPUT_BUFFER_LENGTH         1000u"), "1000 point solver history");
        VerifyShader("Assets/VRCSpice/MNATools/Shader/SolverOutputDump.shader",
            "_MainTex", "_DATA_N", "_DumpGeneration");
        VerifyShader("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerState.shader",
            "_SolverDump", "_PreDump", "_TriggerLevel", "_TriggerMode");
        VerifyShader("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerPre.shader",
            "_SolverDump", "_StateDump");
        VerifyShader("Assets/VRCSpice/Oscilloscope/OscilloscopeTriggerCapture.shader",
            "_SolverDump", "_StateDump", "_PreDump");
        VerifyShader("Assets/VRCSpice/Oscilloscope/OscilloscopeCopy.shader",
            "_SolverDump", "_StateDump", "_CaptureDump", "_SamplingRevision");
        VerifyShader("Assets/VRCSpice/Oscilloscope/OscilloscopeWaveform.shader",
            "_DisplayDump", "_Channel", "_YScale");
        VerifyTriggerContract();
        VerifyGpuTriggerPipeline();
        Mesh point = AssetDatabase.LoadAssetAtPath<Mesh>(PointMeshPath);
        Check(point != null && point.vertexCount == 2 && point.GetTopology(0) == MeshTopology.Points,
            "two-segment waveform point mesh");
        OscilloscopeController controller = GameObject.Find("Oscillo").GetComponent<OscilloscopeController>();
        OscilloscopeTriggerSettingsSync triggerSettingsSync =
            GameObject.Find("Oscillo/TriggerSettingsSync").GetComponent<OscilloscopeTriggerSettingsSync>();
        Check(triggerSettingsSync != null && controller.triggerSettingsSync == triggerSettingsSync &&
            triggerSettingsSync.controller == controller, "shared trigger settings references");
        Check(UdonSharpEditorUtility.GetBackingUdonBehaviour(triggerSettingsSync).SyncMethod ==
            VRC.SDKBase.Networking.SyncType.Manual, "trigger settings use Manual synchronization");
        Check(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(TriggerSyncAsset) != null,
            "trigger settings sync program asset");
        triggerSettingsSync.triggerEnabled = true;
        triggerSettingsSync.triggerMode = 2;
        triggerSettingsSync.triggerSource = 1;
        triggerSettingsSync.triggerFalling = true;
        triggerSettingsSync.triggerLevel = 1.25f;
        triggerSettingsSync.timeDivisionIndex = 3;
        triggerSettingsSync.ch1VoltageDivision = 2f;
        triggerSettingsSync.ch2VoltageDivision = 5f;
        triggerSettingsSync.settingsRevision = 7;
        triggerSettingsSync.OnDeserialization();
        Check(controller.triggerEnableScrollbar.value == 1f &&
            controller.triggerModeScrollbar.value == 1f &&
            controller.triggerSourceDropdown.value == 1 &&
            controller.triggerEdgeScrollbar.value == 1f &&
            controller.triggerLevelField.text == "1.25", "shared trigger settings update all controls");
        Check(Mathf.Approximately(controller.timeDivScrollbar.value, 0.75f) &&
            controller.yScaleField.text == "2" && controller.yScaleField2.text == "5" &&
            Mathf.Approximately(controller.ch1Material.GetFloat("_YScale"), 1f / 16f) &&
            Mathf.Approximately(controller.ch2Material.GetFloat("_YScale"), 1f / 40f),
            "shared view settings update time and channel scales");
        controller.triggerLevelField.SetTextWithoutNotify("NaN");
        controller.ChangeTriggerSettings();
        Check(controller.triggerLevelField.text == "1.25" &&
            Mathf.Approximately(triggerSettingsSync.triggerLevel, 1.25f),
            "invalid trigger level restores the accepted shared value");
        triggerSettingsSync.triggerEnabled = false;
        triggerSettingsSync.triggerMode = 0;
        triggerSettingsSync.triggerSource = 0;
        triggerSettingsSync.triggerFalling = false;
        triggerSettingsSync.triggerLevel = 0f;
        triggerSettingsSync.timeDivisionIndex = 2;
        triggerSettingsSync.ch1VoltageDivision = 5f;
        triggerSettingsSync.ch2VoltageDivision = 5f;
        triggerSettingsSync.OnDeserialization();
        Check(controller.yScaleField != null && controller.yScaleField2 != null &&
            controller.yScaleField.gameObject.name == "InputFieldCH1divy" &&
            controller.yScaleField2.gameObject.name == "InputFieldCH2divy",
            "independent CH1 and CH2 voltage scale fields");
        Check(controller.yScaleField.onSubmit.GetPersistentEventCount() == 1 &&
            controller.yScaleField.onEndEdit.GetPersistentEventCount() == 1 &&
            controller.yScaleField2.onSubmit.GetPersistentEventCount() == 1 &&
            controller.yScaleField2.onEndEdit.GetPersistentEventCount() == 1,
            "voltage scale submit and end-edit events connected once");
        string oldScaleText1 = controller.yScaleField.text;
        string oldScaleText2 = controller.yScaleField2.text;
        controller.yScaleField.SetTextWithoutNotify("2");
        controller.yScaleField2.SetTextWithoutNotify("5");
        controller.ChangeVoltageScale();
        Check(Mathf.Approximately(controller.ch1Material.GetFloat("_YScale"), 1f / 16f) &&
            Mathf.Approximately(controller.ch2Material.GetFloat("_YScale"), 1f / 40f),
            "CH1 and CH2 voltage scales are independent");
        controller.yScaleField.SetTextWithoutNotify("3");
        controller.yScaleField2.SetTextWithoutNotify("4");
        controller.timeDivScrollbar.SetValueWithoutNotify(1f);
        controller.ChangeTimeDivision();
        Check(triggerSettingsSync.timeDivisionIndex == 4 &&
            Mathf.Approximately(triggerSettingsSync.ch1VoltageDivision, 3f) &&
            Mathf.Approximately(triggerSettingsSync.ch2VoltageDivision, 4f) &&
            controller.yScaleField.text == "3" && controller.yScaleField2.text == "4",
            "time division change commits current voltage scale fields");
        controller.yScaleField.SetTextWithoutNotify("2");
        controller.yScaleField2.SetTextWithoutNotify("5");
        controller.ChangeVoltageScale();
        string[] invalidScales = { "0", "-1", "NaN", "Infinity", "", "-" };
        foreach (string invalid in invalidScales)
        {
            controller.yScaleField.SetTextWithoutNotify(invalid);
            controller.ChangeVoltageScale();
            Check(controller.yScaleField.text == "2" &&
                Mathf.Approximately(controller.ch1Material.GetFloat("_YScale"), 1f / 16f),
                "invalid voltage scale restores the previous value: " + invalid);
        }
        controller.yScaleField.SetTextWithoutNotify(oldScaleText1);
        controller.yScaleField2.SetTextWithoutNotify(oldScaleText2);
        controller.ChangeVoltageScale();
        TMP_Text ch1ScaleLabel = GameObject.Find("CH1divVLabel").GetComponent<TMP_Text>();
        TMP_Text ch2ScaleLabel = GameObject.Find("CH2divVLabel").GetComponent<TMP_Text>();
        Check(!ch1ScaleLabel.raycastTarget && !ch2ScaleLabel.raycastTarget,
            "voltage scale labels do not block interaction");
        VerifyShader("Assets/VRCSpice/BreadBoard/Shaders/LedEmission.shader",
            "_MainTex", "_DATA_N", "_OutputDeltaTime");
        PredictorlessSolverVerification.Run(); BreadboardVerification.Run();
        Debug.Log("Oscilloscope HLSL trigger verification PASSED.");
    }
}
