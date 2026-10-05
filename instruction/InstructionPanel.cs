using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class InstructionPanel : UdonSharpBehaviour
{
    public string[] titles = { "説明 1", "説明 2", "説明 3" };
    [TextArea(8, 24)] public string[] bodies = {
        "ここに説明文を書いてください。\n\nInspector の「本文」から編集できます。",
        "2つ目の説明文をここに書いてください。",
        "3つ目の説明文をここに書いてください。"
    };
    [HideInInspector] public TMP_Dropdown dropdown;
    [HideInInspector] public TMP_Text bodyText;
    [HideInInspector] public ScrollRect scrollView;

    private void Start() { ShowSelected(); }

    public void ShowSelected()
    {
        if (dropdown == null || bodyText == null || scrollView == null) return;
        int index = dropdown.value;
        bodyText.text = bodies != null && index >= 0 && index < bodies.Length ? bodies[index] : "";
        // Rebuild before scrolling so a shorter page never retains the previous offset.
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollView.content);
        scrollView.StopMovement();
        scrollView.verticalNormalizedPosition = 1f;
    }
}
