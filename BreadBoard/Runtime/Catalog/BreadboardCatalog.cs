using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardCatalog : UdonSharpBehaviour
{
    public string catalogId = "breadboard-basic";
    public int catalogVersion = 1;
    public string[] kinds = { "Wire", "R", "C", "L", "D", "Q" };
    public string[] kindLabels = { "Wire", "Resistor", "Capacitor", "Inductor", "Diode", "Transistor" };
    public string[] footprints = { "wire-straight", "axial-3p", "radial-2p", "axial-3p", "diode-3p", "bjt-3pin" };
    public string[] modelIds = { "diode-generic", "diode-low-leakage", "KSC1815-fixed" };
    public string[] modelLabels = { "Generic diode", "Low-leakage diode", "KSC1815" };
    public int[] modelKinds = { 4, 4, 5 };
    public float[] diodeIs = { 1e-12f, 1e-14f, 0f };
    public float[] diodeVt = { 0.02585f, 0.02585f, 0f };
    public float[] e24 = { 10,11,12,13,15,16,18,20,22,24,27,30,33,36,39,43,47,51,56,62,68,75,82,91 };
    public float minimumValue = 1e-12f;
    public float maximumValue = 1e12f;
    public Mesh[] bodyMeshes;
    public Material[] bodyMaterials;
    public Material[] leadMaterials;

    public int KindIndex(string name)
    {
        for (int i = 0; i < kinds.Length; i++) if (kinds[i] == name) return i;
        return -1;
    }

    public int ModelIndex(string name, int kind)
    {
        for (int i = 0; i < modelIds.Length; i++)
            if (modelIds[i] == name && modelKinds[i] == kind) return i;
        return -1;
    }

    public int DefaultModel(int kind)
    {
        for (int i = 0; i < modelKinds.Length; i++) if (modelKinds[i] == kind) return i;
        return -1;
    }

    public int NextModel(int kind, int model, int direction)
    {
        int i = model;
        for (int n = 0; n < modelIds.Length; n++)
        {
            i = (i + direction + modelIds.Length) % modelIds.Length;
            if (modelKinds[i] == kind) return i;
        }
        return DefaultModel(kind);
    }

    public int PinCount(int kind) { return kind == 5 ? 3 : 2; }
    public int Span(int kind, int length) { return kind == 0 ? length : (kind == 2 || kind == 5 ? 2 : 3); }
    public string ComponentId(int id) { return "c" + id.ToString("D6"); }
    public float DefaultValue(int kind) { return kind == 2 ? 1e-7f : (kind == 3 ? 1e-3f : 1000f); }

    public float NextValue(int kind, float value, int direction)
    {
        if (kind < 1 || kind > 3 || direction == 0) return value;
        float current = Mathf.Clamp(value,minimumValue,maximumValue);
        float decade = Mathf.Pow(10f,Mathf.Floor(Mathf.Log10(current))-1f);
        float tolerance = current * .00001f;
        if (direction > 0)
        {
            for (int i = 0; i < e24.Length; i++)
                if (e24[i]*decade > current+tolerance) return Mathf.Min(e24[i]*decade,maximumValue);
            return Mathf.Min(100f*decade,maximumValue);
        }
        for (int i = e24.Length-1; i >= 0; i--)
            if (e24[i]*decade < current-tolerance) return Mathf.Max(e24[i]*decade,minimumValue);
        return Mathf.Max(9.1f*decade,minimumValue);
    }

    public bool ValidParameters(int kind, float value, int model, int length)
    {
        if (kind < 0 || kind >= kinds.Length) return false;
        if (kind == 0) return length >= 1 && length <= 30;
        if (kind <= 3) return value > 0f && value <= 1e12f;
        return model >= 0 && model < modelIds.Length && modelKinds[model] == kind;
    }

    public string ParameterLabel(int kind, float value, int model, int length)
    {
        if (kind == 0) return length + " pitches";
        if (kind >= 4) return model >= 0 && model < modelLabels.Length ? modelLabels[model] : "Unknown model";
        string unit = kind == 1 ? "Ohm" : (kind == 2 ? "F" : "H");
        if (value >= 1000000f) return (value / 1000000f).ToString("0.###") + " M" + unit;
        if (value >= 1000f) return (value / 1000f).ToString("0.###") + " k" + unit;
        if (value >= 1f) return value.ToString("0.###") + " " + unit;
        if (value >= 0.001f) return (value * 1000f).ToString("0.###") + " m" + unit;
        if (value >= 0.000001f) return (value * 1000000f).ToString("0.###") + " u" + unit;
        if (value >= 0.000000001f) return (value * 1000000000f).ToString("0.###") + " n" + unit;
        return (value * 1000000000000f).ToString("0.###") + " p" + unit;
    }

    // This is the only model-to-MNAGen conversion boundary. Extend it when MNA
    // gains model inputs; layout, placement and network data need no changes.
    public DataList MnaConstants(int kind, float value, int model)
    {
        DataList constants = new DataList();
        if (kind >= 1 && kind <= 3) constants.Add(value);
        else if (kind == 4) { constants.Add(diodeIs[model]); constants.Add(diodeVt[model]); }
        // Q currently accepts no constants and uses MNAGen's KSC1815 model.
        return constants;
    }
}
