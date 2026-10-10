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
    [HideInInspector] public int circuitRevision;
    [HideInInspector] public int nextSerial = 1;
    [HideInInspector] public float supplyVoltage;
    [HideInInspector] public int integrationScheme;
    [HideInInspector] public float maxDeltaTime = 0.00001f;
    [HideInInspector] public int maxNewtonIterations = 32;
    [HideInInspector] public int solverSettingsRevision = 1;
    [HideInInspector] public int solverHistoryRevision = 1;
    [HideInInspector] public int solverRestartRevision = 1;
    [HideInInspector] public int[] ids, kinds, pin0, pin1, pin2, orientations, lengths, models, occupants;
    [HideInInspector] public float[] values;
    [HideInInspector] public string error = "";
    private bool initialized;

    public void Initialize()
    {
        if (initialized) return;
        layout.Initialize();
        supplyVoltage = layout.supplyVoltage;
        if (integrationScheme < 0 || integrationScheme > 1) integrationScheme = 0;
        if (float.IsNaN(maxDeltaTime) || float.IsInfinity(maxDeltaTime) || maxDeltaTime <= 0f) maxDeltaTime = 0.00001f;
        if (maxNewtonIterations < 1 || maxNewtonIterations > 256) maxNewtonIterations = 32;
        if (solverSettingsRevision < 1) solverSettingsRevision = 1;
        if (solverHistoryRevision < 1) solverHistoryRevision = 1;
        if (solverRestartRevision < 1) solverRestartRevision = 1;
        ids = new int[capacity]; kinds = new int[capacity]; pin0 = new int[capacity];
        pin1 = new int[capacity]; pin2 = new int[capacity]; orientations = new int[capacity];
        lengths = new int[capacity]; models = new int[capacity]; values = new float[capacity];
        occupants = new int[layout.holeIds.Length];
        for (int i = 0; i < occupants.Length; i++) occupants[i] = -1;
        initialized = true;
    }

    private bool CanChangeCircuit()
    {
        return revision < int.MaxValue && circuitRevision < int.MaxValue;
    }
    private void CircuitChanged() { revision++; circuitRevision++; }

    public bool Available(int hole, int ignoredSlot)
    {
        return hole >= 0 && hole < occupants.Length && (occupants[hole] < 0 || occupants[hole] == ignoredSlot);
    }

    public bool CanPlace(int kind, int anchor, int orientation, int length, int ignoredSlot)
    {
        Initialize(); error = "";
        if (!placement.Resolve(kind, anchor, orientation, length)) { error = "A pin has no hole"; return false; }
        if (!Available(placement.pin0, ignoredSlot) || !Available(placement.pin1, ignoredSlot) ||
            (kind == 5 && !Available(placement.pin2, ignoredSlot))) { error = "Hole occupied"; return false; }
        return true;
    }

    public bool Add(int kind, int anchor, int orientation, int length, float value, int model)
    {
        Initialize();
        if (count >= capacity) { error = "Board is full"; return false; }
        if (nextSerial >= int.MaxValue || !CanChangeCircuit()) { error = "Revision limit"; return false; }
        if (kind == 6) { value = 100000000f; model = -1; }
        if (!catalog.ValidParameters(kind, value, model, length)) { error = "Invalid parameter"; return false; }
        if (!CanPlace(kind, anchor, orientation, length, -1)) return false;
        ids[count] = nextSerial++; kinds[count] = kind; orientations[count] = orientation;
        lengths[count] = kind == 0 ? length : 0; values[count] = value; models[count] = model;
        pin0[count] = placement.pin0; pin1[count] = placement.pin1; pin2[count] = placement.pin2;
        count++; CircuitChanged(); RebuildOccupancy(); return true;
    }

    public bool Remove(int slot)
    {
        if (slot < 0 || slot >= count || !CanChangeCircuit()) return false;
        for (int i = slot; i < count - 1; i++)
        {
            ids[i] = ids[i+1]; kinds[i] = kinds[i+1]; pin0[i] = pin0[i+1]; pin1[i] = pin1[i+1];
            pin2[i] = pin2[i+1]; orientations[i] = orientations[i+1]; lengths[i] = lengths[i+1];
            models[i] = models[i+1]; values[i] = values[i+1];
        }
        count--; CircuitChanged(); RebuildOccupancy(); return true;
    }

    public bool ChangeSupply(float voltage)
    {
        Initialize(); error = "";
        if (float.IsNaN(voltage) || float.IsInfinity(voltage) || Mathf.Abs(voltage) > 1e12f)
        { error = "Voltage out of range"; return false; }
        if (!CanChangeCircuit() || voltage == supplyVoltage) return false;
        supplyVoltage = voltage; CircuitChanged(); return true;
    }

    public bool ChangeParameter(int slot, float value, int model)
    {
        if (slot < 0 || slot >= count || !CanChangeCircuit()) return false;
        if (!catalog.ValidParameters(kinds[slot], value, model, lengths[slot])) return false;
        if (values[slot] == value && models[slot] == model) return false;
        values[slot] = value; models[slot] = model; CircuitChanged(); return true;
    }

    public bool ChangeSolverSettings(int scheme, float outputDeltaTime, int newtonIterations)
    {
        Initialize(); error = "";
        if ((scheme != 0 && scheme != 1) || float.IsNaN(outputDeltaTime) ||
            float.IsInfinity(outputDeltaTime) || outputDeltaTime <= 0f ||
            newtonIterations < 1 || newtonIterations > 256)
        { error = "Invalid solver setting"; return false; }
        bool schemeChanged = scheme != integrationScheme;
        bool timeChanged = outputDeltaTime != maxDeltaTime;
        bool newtonChanged = newtonIterations != maxNewtonIterations;
        if (!schemeChanged && !timeChanged && !newtonChanged) return false;
        if (revision >= int.MaxValue || solverSettingsRevision >= int.MaxValue ||
            (timeChanged && solverHistoryRevision >= int.MaxValue))
        { error = "Revision limit"; return false; }
        integrationScheme = scheme; maxDeltaTime = outputDeltaTime;
        maxNewtonIterations = newtonIterations;
        revision++; solverSettingsRevision++;
        if (timeChanged) solverHistoryRevision++;
        return true;
    }

    public bool RestartSimulation()
    {
        Initialize(); error = "";
        if (revision >= int.MaxValue || solverRestartRevision >= int.MaxValue)
        { error = "Revision limit"; return false; }
        revision++; solverRestartRevision++;
        return true;
    }

    public int FindId(int id)
    {
        for (int i = 0; i < count; i++) if (ids[i] == id) return i;
        return -1;
    }

    public bool ChangeGeometry(int slot, int orientation, int length)
    {
        if (slot < 0 || slot >= count || !CanChangeCircuit()) return false;
        int kind = kinds[slot];
        if (!catalog.ValidParameters(kind, values[slot], models[slot], length)) return false;
        if (!CanPlace(kind, pin0[slot], orientation, length, slot)) return false;
        orientations[slot] = orientation; lengths[slot] = kind == 0 ? length : 0;
        pin1[slot] = placement.pin1; pin2[slot] = placement.pin2;
        CircuitChanged(); RebuildOccupancy(); return true;
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
