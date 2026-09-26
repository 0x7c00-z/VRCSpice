using UdonSharp;
using UnityEngine;
using TMPro;

// Temporary primitive visuals. Replace this component with a BreadboardPart subclass
// when a modeled part is available; the manager does not depend on this implementation.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardSimplePart : BreadboardPart
{
    public MeshFilter body;
    public MeshRenderer bodyRenderer;
    public LineRenderer[] leads;
    public TMP_Text label;
    public Transform labelTransform;
    public override void ApplyState(int kind, int anchor, int orientation, int length, float value, int model, bool valid)
    {
        base.ApplyState(kind, anchor, orientation, length, value, model, valid);
        bool preview = isPreview; int id = componentId;
        Vector3 start = layout.holePositions[anchor];
        Vector3 end = placement.PinPosition(kind, anchor, orientation, length, kind == 5 ? 2 : 1);
        Vector3 direction = layout.Direction(orientation);
        Vector3 lift = Vector3.up * (kind == 0 ? 0.009f : 0.014f);
        Material ghost = valid ? previewValid : previewInvalid;
        for (int p = 0; p < 3; p++)
        {
            LineRenderer line = leads[p];
            line.gameObject.SetActive(p == 0 || kind == 5);
            line.sharedMaterial = preview ? ghost : catalog.leadMaterials[kind];
            if (kind == 5)
            {
                Vector3 pin = placement.PinPosition(kind, anchor, orientation, length, p);
                line.positionCount = 2;
                line.SetPosition(0, pin); line.SetPosition(1, pin + lift);
            }
            else if (p == 0)
            {
                line.positionCount = 4;
                line.SetPosition(0, start); line.SetPosition(1, start + lift);
                line.SetPosition(2, end + lift); line.SetPosition(3, end);
            }
        }
        
        body.gameObject.SetActive(kind != 0);
        if (kind != 0)
        {
            body.sharedMesh = catalog.bodyMeshes[kind];
            bodyRenderer.sharedMaterial = preview ? ghost : catalog.bodyMaterials[kind];
            body.transform.localPosition = (start + end) * 0.5f + lift;
            if (kind == 1 || kind == 4)
            {
                body.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
                body.transform.localScale = kind == 1 ? new Vector3(0.006f, 0.008f, 0.006f) : new Vector3(0.0045f, 0.006f, 0.0045f);
            }
            else
            {
                body.transform.localRotation = Quaternion.Euler(0f, -orientation * 90f, 0f);
                body.transform.localScale = kind == 5 ? new Vector3(0.020f, 0.012f, 0.007f) : new Vector3(0.011f, 0.007f, 0.011f);
            }
        }
        labelTransform.localPosition = (start + end) * 0.5f + Vector3.up * 0.027f;
        labelTransform.localRotation = Quaternion.Euler(90f, -orientation * 90f, 0f);
        string title = catalog.kinds[kind] + (preview ? "" : id.ToString());
        string pins = kind == 4 ? "\nA  >  K" : (kind == 5 ? "\nC  B  E" : "");
        label.text = title + "\n" + catalog.ParameterLabel(kind, value, model, length) + pins;
        label.color = preview ? (valid ? new Color(0.5f, 1f, 0.65f, 0.8f) : new Color(1f, 0.4f, 0.3f, 0.8f)) : Color.white;
    }
}
