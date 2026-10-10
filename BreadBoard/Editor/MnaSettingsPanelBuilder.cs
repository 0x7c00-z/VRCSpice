using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using TMPro.EditorUtilities;
using UdonSharp;
using UdonSharpEditor;

public static class MnaSettingsPanelBuilder
{
    private const float DefaultOutputDeltaTime = 0.00001f;
    private const string ScenePath = "Assets/VRCSpice/Scene.unity";
    private const string PrefabPath = "Assets/VRCSpice/BreadBoard/Prefabs/InteractiveBreadboard.prefab";
    private const string PanelScriptPath = "Assets/VRCSpice/BreadBoard/Runtime/UI/MnaSettingsPanel.cs";
    private const string PanelProgramPath = "Assets/VRCSpice/BreadBoard/Runtime/UI/MnaSettingsPanel.asset";

    private static void EnsurePanelProgramAsset()
    {
        UdonSharpProgramAsset program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(PanelProgramPath);
        if (program == null)
        {
            MonoScript source = AssetDatabase.LoadAssetAtPath<MonoScript>(PanelScriptPath);
            if (source == null) throw new System.InvalidOperationException("MnaSettingsPanel.cs was not imported.");
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = source;
            AssetDatabase.CreateAsset(program, PanelProgramPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        UdonSharpProgramAsset.CompileAllCsPrograms(true);
    }

    private static TMP_DefaultControls.Resources StandardResources()
    {
        TMP_DefaultControls.Resources resources = new TMP_DefaultControls.Resources();
        resources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        resources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        resources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        resources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        resources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        resources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        resources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
        return resources;
    }

    private static void Parent(GameObject child, Transform parent, Vector2 position, Vector2 size)
    {
        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        child.layer = parent.gameObject.layer;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject go = TMP_DefaultControls.CreateText(StandardResources());
        go.name = name; Parent(go, parent, position, size);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value; text.fontSize = fontSize; text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false;
        return text;
    }

    private static Transform FindSolverNet()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] candidates = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < candidates.Length; j++)
            {
                if (candidates[j].name != "Net") continue;
                Transform[] descendants = candidates[j].GetComponentsInChildren<Transform>(true);
                for (int k = 0; k < descendants.Length; k++)
                    if (descendants[k].name == "Error InputField") return candidates[j];
            }
        }
        return null;
    }

    private static MNASolve FindSolver()
    {
        Scene scene = SceneManager.GetActiveScene();
        MNASolve[] solvers = Resources.FindObjectsOfTypeAll<MNASolve>();
        for (int i = 0; i < solvers.Length; i++)
            if (solvers[i] != null && solvers[i].gameObject.scene == scene) return solvers[i];
        return null;
    }

    [MenuItem("Tools/VRCSpice/Install solver settings in Net UI")]
    public static void BuildCurrentWorld()
    {
        EnsurePanelProgramAsset();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform net = FindSolverNet();
        MNASolve solver = FindSolver();
        if (net == null || solver == null)
            throw new System.InvalidOperationException("Scene lookup failed: Net=" + (net != null) +
                ", MNASolve=" + (solver != null) + ".");

        BreadboardState state = Object.FindObjectOfType<BreadboardState>(true);
        BreadboardSync sync = Object.FindObjectOfType<BreadboardSync>(true);
        ApplySceneDefaults(solver, state, sync);
        Install(net, solver, state, sync);
        RemoveBreadboardPanel();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Installed solver settings beside Error InputField under Scene/Net.");
    }

    [MenuItem("Tools/VRCSpice/Build and verify predictorless solver")]
    public static void BuildAndVerify()
    {
        BuildCurrentWorld();
        PredictorlessSolverVerification.Run();
        BreadboardVerification.Run();
    }

    private static void RemoveBreadboardPanel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = children.Length - 1; i >= 0; i--)
                if (children[i] != null && (children[i].name == "SolverSettingsCanvas" ||
                    children[i].name == "SolverSettingsPanel")) Object.DestroyImmediate(children[i].gameObject);
            Transform net = root.transform.Find("Net");
            if (net != null && net.childCount == 0) Object.DestroyImmediate(net.gameObject);
            BreadboardState state = root.GetComponentInChildren<BreadboardState>(true);
            BreadboardCatalog catalog = root.GetComponentInChildren<BreadboardCatalog>(true);
            BreadboardPalette palette = root.GetComponentInChildren<BreadboardPalette>(true);
            BreadboardCodec codec = root.GetComponentInChildren<BreadboardCodec>(true);
            BreadboardSync sync = root.GetComponentInChildren<BreadboardSync>(true);
            if (state != null)
            {
                state.Initialize(); state.maxDeltaTime = DefaultOutputDeltaTime;
                if (state.maxNewtonIterations < 1 || state.maxNewtonIterations > 256) state.maxNewtonIterations = 32;
            }
            if (palette != null && catalog != null && palette.kind == 1 && palette.selectedId == 0)
                palette.value = catalog.DefaultValue(1);
            if (sync != null && codec != null && state != null && state.count == 0)
            {
                string snapshot = codec.Encode();
                if (!string.IsNullOrEmpty(snapshot)) sync.snapshot = snapshot;
            }
            CopyProxy(state); CopyProxy(palette); CopyProxy(sync);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ApplySceneDefaults(MNASolve solver, BreadboardState state, BreadboardSync sync)
    {
        solver.SetSolverSettings(solver.GetIntegrationScheme(), DefaultOutputDeltaTime, solver.GetMaxNewtonIterations());
        EditorUtility.SetDirty(solver); CopyProxy(solver);
        if (state == null) return;
        state.Initialize(); state.maxDeltaTime = DefaultOutputDeltaTime;
        BreadboardCatalog catalog = Object.FindObjectOfType<BreadboardCatalog>(true);
        BreadboardPalette palette = Object.FindObjectOfType<BreadboardPalette>(true);
        if (palette != null && catalog != null && palette.kind == 1 && palette.selectedId == 0)
            palette.value = catalog.DefaultValue(1);
        BreadboardCodec codec = Object.FindObjectOfType<BreadboardCodec>(true);
        if (sync != null && codec != null && state.count == 0)
        {
            string snapshot = codec.Encode();
            if (!string.IsNullOrEmpty(snapshot)) sync.snapshot = snapshot;
        }
        CopyProxy(state); CopyProxy(palette); CopyProxy(sync);
    }

    private static void CopyProxy(UdonSharpBehaviour behaviour)
    {
        if (behaviour == null) return;
        UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.All);
        EditorUtility.SetDirty(behaviour);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour));
    }

    private static void AddChangeEvent(UnityEngine.Events.UnityEventBase unityEvent,
        VRC.Udon.UdonBehaviour backing)
    {
        UnityEventTools.AddStringPersistentListener(unityEvent, backing.SendCustomEvent,
            nameof(MnaSettingsPanel.ChangeSettings));
    }

    private static void WireRestartButton(VRC.Udon.UdonBehaviour backing, Transform panel)
    {
        Button restartButton = null;
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        for (int i = 0; i < buttons.Length && restartButton == null; i++)
        {
            TMP_Text[] labels = buttons[i].GetComponentsInChildren<TMP_Text>(true);
            for (int j = 0; j < labels.Length; j++)
                if (labels[j] != null && labels[j].text == "Restart sim")
                { restartButton = buttons[i]; break; }
        }
        if (restartButton == null)
        {
            GameObject buttonObject = TMP_DefaultControls.CreateButton(StandardResources());
            buttonObject.name = "RestartSimulationButton";
            Parent(buttonObject, panel, new Vector2(0f, -125f), new Vector2(270f, 40f));
            restartButton = buttonObject.GetComponent<Button>();
            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = "Restart sim"; label.alignment = TextAlignmentOptions.Center; }
        }
        for (int i = restartButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(restartButton.onClick, i);
        UnityEventTools.AddStringPersistentListener(restartButton.onClick, backing.SendCustomEvent,
            nameof(MnaSettingsPanel.RestartSimulation));
        EditorUtility.SetDirty(restartButton);
    }

    private static void Install(Transform net, MNASolve solver, BreadboardState state, BreadboardSync sync)
    {
        Transform old = net.Find("SolverSettingsPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        old = net.Find("SolverSettingsCanvas");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        RectTransform netRect = net as RectTransform;
        if (netRect != null && netRect.sizeDelta.x < 1300f)
            netRect.sizeDelta = new Vector2(1300f, netRect.sizeDelta.y);
        BoxCollider box = net.GetComponent<BoxCollider>();
        if (box != null && box.size.x < 1.35f) box.size = new Vector3(1.35f, box.size.y, box.size.z);

        GameObject panel = new GameObject("SolverSettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Parent(panel, net, new Vector2(430f, 0f), new Vector2(420f, 300f));
        panel.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.07f, 0.95f);
        Text("Title", panel.transform, "TIME EVOLUTION", new Vector2(0f, 125f), new Vector2(380f, 32f), 24).alignment = TextAlignmentOptions.Center;
        Text("SchemeLabel", panel.transform, "Scheme", new Vector2(-145f, 76f), new Vector2(95f, 34f), 19);
        Text("MaxDtLabel", panel.transform, "max_dt", new Vector2(-145f, 26f), new Vector2(95f, 34f), 19);
        Text("NewtonLabel", panel.transform, "Newton max", new Vector2(-145f, -24f), new Vector2(110f, 34f), 19);

        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(StandardResources());
        dropdownObject.name = "IntegrationSchemeDropdown";
        Parent(dropdownObject, panel.transform, new Vector2(55f, 76f), new Vector2(270f, 40f));
        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions(); dropdown.options.Add(new TMP_Dropdown.OptionData("Radau IIA 5"));
        dropdown.options.Add(new TMP_Dropdown.OptionData("Backward Euler"));
        dropdown.value = state == null ? solver.GetIntegrationScheme() : state.integrationScheme;

        GameObject timeObject = TMP_DefaultControls.CreateInputField(StandardResources());
        timeObject.name = "MaxDeltaTimeInput";
        Parent(timeObject, panel.transform, new Vector2(55f, 26f), new Vector2(270f, 40f));
        TMP_InputField timeInput = timeObject.GetComponent<TMP_InputField>();
        timeInput.contentType = TMP_InputField.ContentType.Standard; timeInput.lineType = TMP_InputField.LineType.SingleLine;
        timeInput.text = (state == null ? solver.GetOutputDeltaTime() : state.maxDeltaTime).ToString("G9");

        GameObject newtonObject = TMP_DefaultControls.CreateInputField(StandardResources());
        newtonObject.name = "MaxNewtonIterationsInput";
        Parent(newtonObject, panel.transform, new Vector2(55f, -24f), new Vector2(270f, 40f));
        TMP_InputField newtonInput = newtonObject.GetComponent<TMP_InputField>();
        newtonInput.contentType = TMP_InputField.ContentType.IntegerNumber; newtonInput.lineType = TMP_InputField.LineType.SingleLine;
        newtonInput.text = (state == null ? solver.GetMaxNewtonIterations() : state.maxNewtonIterations).ToString();

        TMP_Text status = Text("SolverStatus", panel.transform, "", new Vector2(0f, -82f), new Vector2(380f, 42f), 17);
        status.alignment = TextAlignmentOptions.Center;

        MnaSettingsPanel behaviour = panel.AddUdonSharpComponent<MnaSettingsPanel>();
        behaviour.state = state; behaviour.circuitSync = sync; behaviour.solver = solver;
        behaviour.schemeDropdown = dropdown; behaviour.maxDeltaTimeInput = timeInput;
        behaviour.maxNewtonIterationsInput = newtonInput; behaviour.statusText = status;
        UdonSharpEditorUtility.CopyProxyToUdon(behaviour, ProxySerializationPolicy.All);
        VRC.Udon.UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
        AddChangeEvent(dropdown.onValueChanged, backing);
        AddChangeEvent(timeInput.onSubmit, backing); AddChangeEvent(timeInput.onEndEdit, backing);
        AddChangeEvent(newtonInput.onSubmit, backing); AddChangeEvent(newtonInput.onEndEdit, backing);
        WireRestartButton(backing, panel.transform);
        if (dropdown.onValueChanged.GetPersistentEventCount() < 1 ||
            timeInput.onSubmit.GetPersistentEventCount() < 1 || timeInput.onEndEdit.GetPersistentEventCount() < 1 ||
            newtonInput.onSubmit.GetPersistentEventCount() < 1 || newtonInput.onEndEdit.GetPersistentEventCount() < 1)
            throw new System.InvalidOperationException("TIME EVOLUTION UI events were not connected.");
        EditorUtility.SetDirty(behaviour);
    }
}
