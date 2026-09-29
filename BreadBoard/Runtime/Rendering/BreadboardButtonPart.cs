using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardButtonPart : BreadboardPart
{
    public SkinnedMeshRenderer buttonRenderer;
    public Transform modelRoot;
    public BoxCollider interactCube;
    public Material[] normalMaterials;
    private BreadboardSync circuitSync;
    private bool locallyHeld;
    private int gesture, inputHand = -1, lastHand = -1;
    private int lastInputFrame = -100;
    private float nextHeartbeat;
    private Material[] ghostMaterials;

    public override void InitializePart(BreadboardLayout boardLayout, BreadboardCatalog boardCatalog,
        BreadboardPlacement boardPlacement, int id, bool preview, Material valid, Material invalid)
    {
        base.InitializePart(boardLayout,boardCatalog,boardPlacement,id,preview,valid,invalid);
        BreadboardController controller = boardLayout.GetComponent<BreadboardController>();
        circuitSync = controller == null ? null : controller.circuitSync;
        interactCube.enabled = !preview;
    }
    public override void ApplyState(int kind, int anchor, int orientation, int length, float value, int model, bool valid)
    {
        modelRoot.localPosition = layout.holePositions[anchor];
        modelRoot.localRotation = Quaternion.Euler(0,-orientation*90f,0);
        // Unity imports Blender's 0..1 shape range as weights 0..100.
        buttonRenderer.SetBlendShapeWeight(0,!isPreview && value == .05f ? 100f : 0f);
        interactCube.center = layout.holePositions[anchor] + Quaternion.Euler(0,-orientation*90f,0)*new Vector3(.01f,.015f,0);
        interactCube.size = orientation%2==0 ? new Vector3(.0125f,.011f,.012f) : new Vector3(.012f,.011f,.0125f);
        interactCube.enabled = !isPreview;
        if (isPreview)
        {
            if (ghostMaterials == null) ghostMaterials = new Material[normalMaterials.Length];
            for(int i=0;i<ghostMaterials.Length;i++)ghostMaterials[i]=valid?previewValid:previewInvalid;
            buttonRenderer.sharedMaterials=ghostMaterials;
        }
        else buttonRenderer.sharedMaterials=normalMaterials;
    }
    public override void Interact()
    {
        if (!initialized || isPreview || circuitSync == null || locallyHeld) return;
        locallyHeld=true;inputHand=lastInputFrame >= Time.frameCount-1 ? lastHand : -1;gesture++;nextHeartbeat=0;
        circuitSync.RequestButton(componentId,true,gesture);
    }
    public override void InputUse(bool value, UdonInputEventArgs args)
    {
        int hand = args.handType == HandType.LEFT ? 0 : 1;
        if(value) {lastHand=hand;lastInputFrame=Time.frameCount; if(locallyHeld && inputHand<0)inputHand=hand;}
        else if(locallyHeld && (inputHand<0 || inputHand==hand)) ReleaseHeld();
    }
    private void Update()
    {
        if(!initialized || isPreview || circuitSync==null)return;
        // Erase/edit tools take precedence over interacting with an existing button.
        interactCube.enabled = !(circuitSync.CanEdit() && circuitSync.controller.palette.mode != 0);
        if(locallyHeld && Time.time >= nextHeartbeat)
        {
            nextHeartbeat=Time.time+.4f;
            circuitSync.RequestButton(componentId,true,gesture);
        }
    }
    private void ReleaseHeld()
    {
        if(!locallyHeld)return;
        locallyHeld=false;inputHand=-1;
        if(circuitSync!=null)circuitSync.RequestButton(componentId,false,gesture);
    }
    private void OnDisable() { ReleaseHeld(); }
    public override void ReleasePart() { ReleaseHeld(); base.ReleasePart(); }
}
