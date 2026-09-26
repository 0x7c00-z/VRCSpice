using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardRenderer : UdonSharpBehaviour
{
    public BreadboardState state;
    public BreadboardLayout layout;
    public BreadboardCatalog catalog;
    public BreadboardPlacement placement;
    public Transform partsRoot;
    public Material previewValid, previewInvalid;
    public Transform pointer;
    private BreadboardPart[] instances;
    private int[] instanceIds, instanceKinds;
    private BreadboardPart previewPart;
    private int previewKind = -1;
    private int lastAnchor = -1, lastKind = -1, lastOrientation, lastLength, lastModel;
    private float lastValue;
    private bool lastValid;

    public BreadboardPart FindPart(int id)
    {
        if (instances == null) return null;
        for (int i = 0; i < instances.Length; i++)
            if (instances[i] != null && instanceIds[i] == id) return instances[i];
        return null;
    }

    private BreadboardPart CreatePart(int kind, int id, bool preview)
    {
        if (kind < 0 || catalog.partPrefabs == null || kind >= catalog.partPrefabs.Length || catalog.partPrefabs[kind] == null)
        { Debug.LogError("Breadboard: missing part prefab for kind " + kind); return null; }
        GameObject go = Instantiate(catalog.partPrefabs[kind]);
        go.SetActive(false);
        go.transform.SetParent(partsRoot, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        BreadboardPart part = go.GetComponent<BreadboardPart>();
        if (part == null) { Debug.LogError("Breadboard: prefab requires BreadboardPart on root"); Destroy(go); return null; }
        go.name = preview ? "Preview" : "Part_" + id;
        part.InitializePart(layout, catalog, placement, id, preview, previewValid, previewInvalid);
        return part;
    }

    private void RemovePart(BreadboardPart part)
    {
        if (part == null) return;
        part.gameObject.SetActive(false); part.ReleasePart(); Destroy(part.gameObject);
    }

    public void Rebuild()
    {
        state.Initialize();
        if (instances == null)
        {
            // Only references are reserved; GameObjects are created on demand.
            instances = new BreadboardPart[state.capacity];
            instanceIds = new int[state.capacity]; instanceKinds = new int[state.capacity];
        }
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i] == null) continue;
            int slot = state.FindId(instanceIds[i]);
            if (slot < 0 || state.kinds[slot] != instanceKinds[i])
            { RemovePart(instances[i]); instances[i] = null; instanceIds[i] = 0; }
        }
        for (int slot = 0; slot < state.count; slot++)
        {
            BreadboardPart part = FindPart(state.ids[slot]);
            if (part == null)
            {
                part = CreatePart(state.kinds[slot], state.ids[slot], false);
                if (part == null) continue;
                for (int i = 0; i < instances.Length; i++) if (instances[i] == null)
                { instances[i] = part; instanceIds[i] = state.ids[slot]; instanceKinds[i] = state.kinds[slot]; break; }
            }
            part.ApplyState(state.kinds[slot], state.pin0[slot], state.orientations[slot], state.lengths[slot], state.values[slot], state.models[slot], true);
            part.gameObject.SetActive(true);
        }
        HidePreview();
    }

    public void HidePreview()
    {
        if (previewPart != null) previewPart.gameObject.SetActive(false);
        lastAnchor = -1;
    }

    public void Preview(int kind, int anchor, int orientation, int length, float value, int model, bool valid)
    {
        if (anchor < 0) { HidePreview(); return; }
        if (anchor == lastAnchor && kind == lastKind && orientation == lastOrientation && length == lastLength &&
            value == lastValue && model == lastModel && valid == lastValid) return;
        if (previewPart == null || previewKind != kind)
        {
            RemovePart(previewPart); previewPart = CreatePart(kind, 0, true); previewKind = kind;
        }
        if (previewPart == null) return;
        previewPart.ApplyState(kind, anchor, orientation, length, value, model, valid);
        previewPart.gameObject.SetActive(true);
        lastAnchor = anchor; lastKind = kind; lastOrientation = orientation; lastLength = length;
        lastValue = value; lastModel = model; lastValid = valid;
    }

    public void ShowPointer(Vector3 local, bool show)
    {
        pointer.gameObject.SetActive(show);
        if (show) pointer.localPosition = new Vector3(local.x, 0.004f, local.z);
    }
}
