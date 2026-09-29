using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardResistorPart : BreadboardPart
{
    public Transform modelRoot;
    public MeshRenderer resistorRenderer;
    public TMP_Text fallbackLabel;
    public Transform labelTransform;
    public Material bodyMaterial, metalMaterial;
    // Digits 0..9, gold multiplier (-1), silver multiplier (-2).
    public Material[] codeMaterials;
    public int bodySlot, metalSlot = 5;
    public int[] bandSlots = { 1, 2, 4, 3 };
    [HideInInspector] public int firstDigit, secondDigit, multiplier;
    private Material[] slots;

    public bool TryColorCode(float value)
    {
        firstDigit = -1; secondDigit = -1; multiplier = -99;
        if (!(value > 0f) || value < .0999998f || value > 99.0002e9f) return false;
        for (int exponent = -2; exponent <= 9; exponent++)
        {
            float scale = Mathf.Pow(10f,exponent);
            float digits = value / scale;
            if (digits < 9.99998f || digits > 99.00002f) continue;
            int rounded = Mathf.RoundToInt(digits);
            // Allow floating point arithmetic noise, not rounding to another resistance.
            if (rounded < 10 || rounded > 99 || Mathf.Abs(value-rounded*scale) > value*0.000002f) continue;
            firstDigit = rounded / 10; secondDigit = rounded % 10; multiplier = exponent;
            return true;
        }
        return false;
    }

    public override void ApplyState(int kind, int anchor, int orientation, int length, float value, int model, bool valid)
    {
        modelRoot.localPosition = layout.holePositions[anchor];
        modelRoot.localRotation = Quaternion.Euler(0,-orientation*90f,0);
        bool encoded = TryColorCode(value);
        if (slots == null) slots = new Material[resistorRenderer.sharedMaterials.Length];
        Material ghost = valid ? previewValid : previewInvalid;
        for (int i = 0; i < slots.Length; i++) slots[i] = isPreview ? ghost : bodyMaterial;
        if (!isPreview)
        {
            slots[bodySlot] = bodyMaterial; slots[metalSlot] = metalMaterial;
            if (encoded)
            {
                slots[bandSlots[0]] = codeMaterials[firstDigit];
                slots[bandSlots[1]] = codeMaterials[secondDigit];
                slots[bandSlots[2]] = codeMaterials[multiplier >= 0 ? multiplier : (multiplier == -1 ? 10 : 11)];
                slots[bandSlots[3]] = codeMaterials[10]; // Cosmetic fixed gold band; no simulated tolerance.
            }
        }
        resistorRenderer.sharedMaterials = slots;
        fallbackLabel.gameObject.SetActive(!encoded);
        if (!encoded)
        {
            // Display the value instead of inventing a rounded color code.
            fallbackLabel.text = value.ToString("G7") + " Ohm";
            fallbackLabel.color = isPreview ? (valid ? new Color(.5f,1,.65f,.8f) : new Color(1,.4f,.3f,.8f)) : Color.white;
            labelTransform.localPosition = layout.holePositions[anchor] + layout.Direction(orientation)*(layout.pitch*1.5f) + Vector3.up*.045f;
            labelTransform.localRotation = Quaternion.Euler(90,-orientation*90f,0);
        }
    }
}
