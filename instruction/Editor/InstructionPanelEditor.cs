using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UdonSharpEditor;

[CustomEditor(typeof(InstructionPanel))]
public class InstructionPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        serializedObject.Update();
        var titles = serializedObject.FindProperty("titles");
        var bodies = serializedObject.FindProperty("bodies");
        EditorGUILayout.HelpBox("項目名と本文を編集してください。選択肢は自動で更新されます。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        int count = Mathf.Max(0, EditorGUILayout.IntField("項目数", titles.arraySize));
        titles.arraySize = count;
        bodies.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.PropertyField(titles.GetArrayElementAtIndex(i), new GUIContent("項目名 " + (i + 1)));
            EditorGUILayout.LabelField("本文");
            var body = bodies.GetArrayElementAtIndex(i);
            body.stringValue = EditorGUILayout.TextArea(body.stringValue, GUILayout.MinHeight(110));
            EditorGUILayout.EndVertical();
        }
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            Sync((InstructionPanel)target);
        }
        else serializedObject.ApplyModifiedProperties();
    }

    public static void Sync(InstructionPanel panel)
    {
        if (!panel.dropdown) return;
        Undo.RecordObject(panel.dropdown, "Edit instruction pages");
        Undo.RecordObject(panel.bodyText, "Edit instruction preview");
        panel.dropdown.options.Clear();
        foreach (string title in panel.titles)
            panel.dropdown.options.Add(new TMP_Dropdown.OptionData(title ?? ""));
        panel.dropdown.SetValueWithoutNotify(0);
        panel.dropdown.RefreshShownValue();
        panel.dropdown.interactable = panel.titles.Length > 0;
        panel.ShowSelected();
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(panel.dropdown);
        EditorUtility.SetDirty(panel.bodyText);
        UdonSharpEditorUtility.CopyProxyToUdon(panel, ProxySerializationPolicy.All);
        if (panel.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
    }
}
