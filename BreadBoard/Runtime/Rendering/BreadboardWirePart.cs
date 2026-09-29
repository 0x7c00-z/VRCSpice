using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardWirePart : BreadboardPart
{
    public SkinnedMeshRenderer wireRenderer;
    public Material metalMaterial;
    public Material[] lengthMaterials;
    public int metalSlot, colorSlot = 1;
    public LineRenderer legacyLine;
    public TMP_Text legacyLabel;
    public Transform labelTransform;
    private Material[] slots;

    public override void ApplyState(int kind, int anchor, int orientation, int length, float value, int model, bool valid)
    {
        bool modeled = length >= 1 && length <= 9;
        wireRenderer.gameObject.SetActive(modeled);
        legacyLine.gameObject.SetActive(!modeled);
        legacyLabel.gameObject.SetActive(!modeled);
        Vector3 start = layout.holePositions[anchor];
        Material ghost = valid ? previewValid : previewInvalid;
        if (modeled)
        {
            wireRenderer.transform.localPosition = start;
            wireRenderer.transform.localRotation = Quaternion.Euler(0,-orientation*90f,0);
            wireRenderer.transform.localScale = Vector3.one * (layout.pitch/.01f);
            wireRenderer.SetBlendShapeWeight(0,(length-1)*100f/9f);
            if (slots == null) slots = new Material[2];
            slots[metalSlot] = isPreview ? ghost : metalMaterial;
            slots[colorSlot] = isPreview ? ghost : lengthMaterials[length-1];
            wireRenderer.sharedMaterials = slots;
        }
        else
        {
            Vector3 end = start + layout.Direction(orientation)*layout.pitch*length;
            legacyLine.sharedMaterial = isPreview ? ghost : catalog.leadMaterials[0];
            legacyLine.positionCount = 4;
            legacyLine.SetPosition(0,start); legacyLine.SetPosition(1,start+Vector3.up*.009f);
            legacyLine.SetPosition(2,end+Vector3.up*.009f); legacyLine.SetPosition(3,end);
            legacyLabel.text = "Wire" + (isPreview ? "" : componentId.ToString()) + "\n" + length + " pitches";
            legacyLabel.color = isPreview ? new Color(.5f,1,.65f,.8f) : Color.white;
            labelTransform.localPosition = (start+end)*.5f+Vector3.up*.027f;
            labelTransform.localRotation = Quaternion.Euler(90,-orientation*90f,0);
        }
    }
}
