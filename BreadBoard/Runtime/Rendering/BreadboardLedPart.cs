using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class BreadboardLedPart : BreadboardPart
{
    public Transform modelRoot;
    public MeshRenderer ledRenderer;
    public Material[] normalMaterials;
    public int emitterSlot = 2;
    [HideInInspector] public int currentRow = -1;
    private Material[] slots;
    private MaterialPropertyBlock properties;

    public override void ApplyState(int kind,int anchor,int orientation,int length,float value,int model,bool valid)
    {
        modelRoot.localPosition=layout.holePositions[anchor];
        modelRoot.localRotation=Quaternion.Euler(0,-orientation*90f,0);
        if(slots==null)slots=new Material[normalMaterials.Length];
        for(int i=0;i<slots.Length;i++)slots[i]=isPreview?(valid?previewValid:previewInvalid):normalMaterials[i];
        if(!isPreview && emitterMaterial!=null)slots[emitterSlot]=emitterMaterial;
        ledRenderer.sharedMaterials=slots;
    }
    public override void BindSolver(MNASolve activeSolver)
    {
        base.BindSolver(activeSolver);
        currentRow=-1;
        if(!isPreview && solver!=null)currentRow=solver.label2bufferRow("I_D_"+catalog.ComponentId(componentId)+"_0");
        if(properties==null)properties=new MaterialPropertyBlock();
        properties.SetInteger("_CurrentRow",currentRow);
        ledRenderer.SetPropertyBlock(properties);
    }
    public override void ReleasePart()
    {
        currentRow=-1;ledRenderer.SetPropertyBlock(null);emitterMaterial=null;base.ReleasePart();
    }
}
