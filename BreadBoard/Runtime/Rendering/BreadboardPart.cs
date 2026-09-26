using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardPart : UdonSharpBehaviour
{
    [HideInInspector] public BreadboardLayout layout;
    [HideInInspector] public BreadboardCatalog catalog;
    [HideInInspector] public BreadboardPlacement placement;
    [HideInInspector] public int componentId;
    [HideInInspector] public bool isPreview;
    [HideInInspector] public bool initialized;
    [HideInInspector] public Material previewValid, previewInvalid;

    // Explicit initialization: Start ordering is not used for binding a new instance.
    public virtual void InitializePart(BreadboardLayout boardLayout, BreadboardCatalog boardCatalog,
        BreadboardPlacement boardPlacement, int id, bool preview, Material valid, Material invalid)
    {
        layout = boardLayout; catalog = boardCatalog; placement = boardPlacement;
        componentId = id; isPreview = preview; previewValid = valid; previewInvalid = invalid;
        initialized = true;
    }

    // Coordinates are in board-local space. Derived classes own all visual changes.
    // This method must not modify circuit state, especially when isPreview is true.
    public virtual void ApplyState(int kind, int anchor, int orientation, int length,
        float value, int model, bool valid) { }

    // Release any per-instance resources here. Called before replacement/deletion.
    public virtual void ReleasePart() { initialized = false; }
}
