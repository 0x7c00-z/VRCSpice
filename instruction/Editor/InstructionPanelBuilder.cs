using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UdonSharpEditor;

public static class InstructionPanelBuilder
{
    static void Rect(RectTransform r, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
    { r.anchorMin = min; r.anchorMax = max; r.offsetMin = lo; r.offsetMax = hi; }

    public static string Build()
    {
        var canvas = GameObject.Find("instruction");
        if (!canvas || !canvas.GetComponent<Canvas>()) throw new System.Exception("instruction Canvas not found");
        if (canvas.transform.Find("InstructionPanel")) return "Panel already exists; no changes made.";
        var fontPath = "Assets/instruction/ZenMaruGothic-Regular.ttf";
        if (!AssetDatabase.LoadAssetAtPath<Font>(fontPath))
            AssetDatabase.CopyAsset("Packages/net.kwxxw.yama-stream/Assets/Fonts/ZenMaruGothic-Regular.ttf", fontPath);
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        var res = new DefaultControls.Resources {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
        var root = new GameObject("InstructionPanel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create instruction panel");
        root.transform.SetParent(canvas.transform, false);
        Rect((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.GetComponent<Image>().color = new Color(.09f, .12f, .17f, .98f);

        var drop = DefaultControls.CreateDropdown(res);
        drop.name = "Dropdown";
        drop.transform.SetParent(root.transform, false);
        Rect((RectTransform)drop.transform, new Vector2(0,1), Vector2.one, new Vector2(24,-82), new Vector2(-24,-24));
        var dropdown = InstructionPanelTMPUpgrade.ConvertDropdown(drop, InstructionPanelTMPUpgrade.GetFont());
        drop.transform.Find("Arrow").GetComponent<Image>().color = new Color(.12f,.16f,.22f);
        dropdown.template.Find("Viewport/Content/Item/Item Checkmark").GetComponent<Image>().color = new Color(.12f,.16f,.22f);
        dropdown.template.sizeDelta = new Vector2(0, 240);
        dropdown.template.Find("Viewport/Content").GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
        dropdown.template.Find("Viewport/Content/Item").GetComponent<RectTransform>().sizeDelta = new Vector2(0, 52);
        dropdown.captionText.rectTransform.offsetMin = new Vector2(18,6);
        dropdown.captionText.rectTransform.offsetMax = new Vector2(-48,-6);

        var scrollGo = DefaultControls.CreateScrollView(res);
        scrollGo.transform.SetParent(root.transform, false);
        Rect((RectTransform)scrollGo.transform, Vector2.zero, Vector2.one, new Vector2(24,24), new Vector2(-24,-102));
        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
        scroll.horizontalScrollbar = null;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        scrollGo.GetComponent<Image>().color = new Color(.96f,.97f,.99f);
        Rect(scroll.viewport, Vector2.zero, Vector2.one, new Vector2(20,16), new Vector2(-36,-16));
        Rect((RectTransform)scroll.verticalScrollbar.transform, new Vector2(1,0), Vector2.one, new Vector2(-18,8), new Vector2(-4,-8));
        scroll.verticalScrollbar.targetGraphic.color = new Color(.35f,.47f,.60f);
        var content = scroll.content;
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f,1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var body = content.gameObject.AddComponent<TextMeshProUGUI>();
        body.font = InstructionPanelTMPUpgrade.GetFont(); body.fontSize = 24; body.color = new Color(.12f,.16f,.22f);
        body.alignment = TextAlignmentOptions.TopLeft; body.enableWordWrapping = true;
        body.overflowMode = TextOverflowModes.Overflow; body.richText = true; body.lineSpacing = 6f;
        body.raycastTarget = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        foreach (var text in drop.GetComponentsInChildren<TMP_Text>(true))
        { text.font = InstructionPanelTMPUpgrade.GetFont(); text.fontSize = 26; text.color = new Color(.12f,.16f,.22f); text.raycastTarget = false; }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = canvas.layer;
        drop.transform.SetAsLastSibling();

        var panel = root.AddUdonSharpComponent<InstructionPanel>();
        panel.dropdown = dropdown; panel.bodyText = body; panel.scrollView = scroll;
        var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(panel);
        UnityEventTools.AddStringPersistentListener(dropdown.onValueChanged, backing.SendCustomEvent, "ShowSelected");
        InstructionPanelEditor.Sync(panel);
        if (!canvas.GetComponent<BoxCollider>())
        {
            var collider = Undo.AddComponent<BoxCollider>(canvas);
            var r = ((RectTransform)canvas.transform).rect;
            collider.size = new Vector3(r.width, r.height, 1f);
            collider.center = new Vector3(r.center.x, r.center.y, 0);
        }
        Canvas.ForceUpdateCanvases();
        AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/instruction/InstructionPanel.prefab");
        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Selection.activeGameObject = root;
        return "Created instruction/InstructionPanel and saved scene + prefab.";
    }
}
