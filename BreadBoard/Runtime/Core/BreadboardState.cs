using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardState : UdonSharpBehaviour
{
    public int capacity = 128;
    public BreadboardLayout layout;
    public BreadboardCatalog catalog;
    public BreadboardPlacement placement;
    [HideInInspector] public int count;
    [HideInInspector] public int revision;
    [HideInInspector] public int nextSerial = 1;
    [HideInInspector] public float supplyVoltage;
    [HideInInspector] public int[] ids, kinds, pin0, pin1, pin2, orientations, lengths, models, occupants;
    [HideInInspector] public float[] values;
    [HideInInspector] public string error = "";
    private bool initialized;

    public void Initialize()
    {
        if (initialized) return;
        layout.Initialize();
        supplyVoltage = layout.supplyVoltage;
        ids = new int[capacity]; kinds = new int[capacity]; pin0 = new int[capacity];
        pin1 = new int[capacity]; pin2 = new int[capacity]; orientations = new int[capacity];
        lengths = new int[capacity]; models = new int[capacity]; values = new float[capacity];
        occupants = new int[layout.holeIds.Length];
        for (int i = 0; i < occupants.Length; i++) occupants[i] = -1;
        initialized = true;
    }

    public bool Available(int hole, int ignoredSlot)
    {
        return hole >= 0 && hole < occupants.Length && (occupants[hole] < 0 || occupants[hole] == ignoredSlot);
    }

    public bool CanPlace(int kind, int anchor, int orientation, int length, int ignoredSlot)
    {
        Initialize();
        error = "";
        if (!placement.Resolve(kind, anchor, orientation, length)) { error = "A pin has no hole"; return false; }
        if (!Available(placement.pin0, ignoredSlot) || !Available(placement.pin1, ignoredSlot) ||
            (kind == 5 && !Available(placement.pin2, ignoredSlot))) { error = "Hole occupied"; return false; }
        return true;
    }

    public bool Add(int kind, int anchor, int orientation, int length, float value, int model)
    {
        Initialize();
        if (count >= capacity) { error = "Board is full"; return false; }
        if (nextSerial >= int.MaxValue || revision >= int.MaxValue) { error = "Revision limit"; return false; }
        if (!catalog.ValidParameters(kind, value, model, length)) { error = "Invalid parameter"; return false; }
        if (!CanPlace(kind, anchor, orientation, length, -1)) return false;
        ids[count] = nextSerial++; kinds[count] = kind; orientations[count] = orientation;
        lengths[count] = kind == 0 ? length : 0; values[count] = value; models[count] = model;
        pin0[count] = placement.pin0; pin1[count] = placement.pin1; pin2[count] = placement.pin2;
        count++; revision++; RebuildOccupancy(); return true;
    }

    public bool Remove(int slot)
    {
        if (slot < 0 || slot >= count || revision >= int.MaxValue) return false;
        for (int i = slot; i < count - 1; i++)
        {
            ids[i] = ids[i+1]; kinds[i] = kinds[i+1]; pin0[i] = pin0[i+1]; pin1[i] = pin1[i+1];
            pin2[i] = pin2[i+1]; orientations[i] = orientations[i+1]; lengths[i] = lengths[i+1];
            models[i] = models[i+1]; values[i] = values[i+1];
        }
        count--; revision++; RebuildOccupancy(); return true;
    }

    public bool ChangeSupply(float voltage)
    {
        Initialize(); error = "";
        if (float.IsNaN(voltage) || float.IsInfinity(voltage) || Mathf.Abs(voltage) > 1e12f)
        { error = "Voltage out of range"; return false; }
        if (revision >= int.MaxValue || voltage == supplyVoltage) return false;
        supplyVoltage = voltage; revision++; return true;
    }

    public bool ChangeParameter(int slot, float value, int model)
    {
        if (slot < 0 || slot >= count || revision >= int.MaxValue) return false;
        if (!catalog.ValidParameters(kinds[slot], value, model, lengths[slot])) return false;
        if (values[slot] == value && models[slot] == model) return false;
        values[slot] = value; models[slot] = model; revision++; return true;
    }

    public int FindId(int id)
    {
        for (int i = 0; i < count; i++) if (ids[i] == id) return i;
        return -1;
    }

    public bool ChangeGeometry(int slot, int orientation, int length)
    {
        if (slot < 0 || slot >= count || revision >= int.MaxValue) return false;
        int kind = kinds[slot];
        if (!catalog.ValidParameters(kind, values[slot], models[slot], length)) return false;
        if (!CanPlace(kind, pin0[slot], orientation, length, slot)) return false;
        orientations[slot] = orientation; lengths[slot] = kind == 0 ? length : 0;
        pin1[slot] = placement.pin1; pin2[slot] = placement.pin2;
        revision++; RebuildOccupancy(); return true;
    }

    public void RebuildOccupancy()
    {
        for (int i = 0; i < occupants.Length; i++) occupants[i] = -1;
        for (int i = 0; i < count; i++)
        {
            occupants[pin0[i]] = i; occupants[pin1[i]] = i;
            if (pin2[i] >= 0) occupants[pin2[i]] = i;
        }
    }
}
