using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using UdonSharpEditor;

public static class InstructionPanelTMPUpgrade
{
    public static TMP_FontAsset GetFont()
    {
        const string path = "Assets/instruction/ZenMaruGothic TMP.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font) return font;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/instruction/ZenMaruGothic-Regular.ttf");
        font = TMP_FontAsset.CreateFontAsset(source, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = "ZenMaruGothic TMP";
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        EditorUtility.SetDirty(font);
        return font;
    }

    public static TMP_Text ConvertText(Text old, TMP_FontAsset font)
    {
        if (!old) return null;
        var go = old.gameObject;
        var text = old.text; var size = old.fontSize; var color = old.color;
        var alignment = old.alignment; var raycast = old.raycastTarget;
        var rich = old.supportRichText; var wrap = old.horizontalOverflow == HorizontalWrapMode.Wrap;
        var spacing = old.lineSpacing; var style = old.fontStyle;
        Object.DestroyImmediate(old);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font; tmp.fontSize = size; tmp.color = color; tmp.text = text;
        tmp.raycastTarget = raycast; tmp.richText = rich; tmp.enableWordWrapping = wrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.lineSpacing = (spacing - 1f) * size;
        var alignments = new[] {TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top, TextAlignmentOptions.TopRight,
            TextAlignmentOptions.Left, TextAlignmentOptions.Center, TextAlignmentOptions.Right,
            TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom, TextAlignmentOptions.BottomRight};
        tmp.alignment = alignments[(int)alignment];
        tmp.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : style == FontStyle.Italic ? FontStyles.Italic :
            style == FontStyle.BoldAndItalic ? FontStyles.Bold | FontStyles.Italic : FontStyles.Normal;
        return tmp;
    }

    public static TMP_Dropdown ConvertDropdown(GameObject go, TMP_FontAsset font)
    {
        var existing = go.GetComponent<TMP_Dropdown>();
        if (existing) return existing;
        var old = go.GetComponent<Dropdown>();
        var json = EditorJsonUtility.ToJson(old);
        var caption = old.captionText.gameObject; var item = old.itemText.gameObject;
        var template = old.template;
        Object.DestroyImmediate(old);
        foreach (var text in go.GetComponentsInChildren<Text>(true)) ConvertText(text, font);
        var dropdown = go.AddComponent<TMP_Dropdown>();
        EditorJsonUtility.FromJsonOverwrite(json, dropdown);
        dropdown.captionText = caption.GetComponent<TMP_Text>();
        dropdown.itemText = item.GetComponent<TMP_Text>();
        dropdown.template = template;
        dropdown.targetGraphic = go.GetComponent<Image>();
        dropdown.RefreshShownValue();
        return dropdown;
    }

    public static void Upgrade(GameObject root)
    {
        var panel = root.GetComponent<InstructionPanel>();
        var font = GetFont();
        // Prewarm current content; dynamic multi-atlas also supports later edits.
        string missing;
        font.TryAddCharacters(string.Join("\n", panel.titles) + string.Join("\n", panel.bodies), out missing);
        if (!string.IsNullOrEmpty(missing.Replace("\n", "").Replace("\r", "")))
            Debug.LogWarning("Instruction font missing characters: " + missing);
        panel.dropdown = ConvertDropdown(root.transform.Find("Dropdown").gameObject, font);
        panel.dropdown.targetGraphic = panel.dropdown.GetComponent<Image>();
        panel.dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(panel);
        UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
            panel.dropdown.onValueChanged, backing.SendCustomEvent, "ShowSelected");
        foreach (var text in root.GetComponentsInChildren<Text>(true)) ConvertText(text, font);
        panel.bodyText = root.transform.Find("Scroll View/Viewport/Content").GetComponent<TMP_Text>();
        panel.scrollView = root.transform.Find("Scroll View").GetComponent<ScrollRect>();
        InstructionPanelEditor.Sync(panel);
        EditorUtility.SetDirty(font);
    }

    public static string UpgradeAll()
    {
        var panel = Object.FindObjectOfType<InstructionPanel>();
        if (!panel) throw new System.Exception("InstructionPanel not found");
        Upgrade(panel.gameObject);
        const string prefabPath = "Assets/instruction/InstructionPanel.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try { Upgrade(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(panel.gameObject.scene);
        Selection.activeGameObject = panel.bodyText.gameObject;
        return "Scene and prefab migrated independently, preserving each set of pages.";
    }
}
