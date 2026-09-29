using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

public static class BreadboardButtonVerification
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);checks++;}
    [MenuItem("Tools/Breadboard/Verify button data and model")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play");checks=0;
        var go=new GameObject("Button verification");GameObject view=null;
        try {
            var layout=go.AddUdonSharpComponent<BreadboardLayout>();BreadboardBuilder.ConfigureLayout(layout);
            var catalog=go.AddUdonSharpComponent<BreadboardCatalog>();
            var placement=go.AddUdonSharpComponent<BreadboardPlacement>();placement.layout=layout;placement.catalog=catalog;
            var state=go.AddUdonSharpComponent<BreadboardState>();state.layout=layout;state.catalog=catalog;state.placement=placement;
            var codec=go.AddUdonSharpComponent<BreadboardCodec>();codec.state=state;codec.layout=layout;codec.catalog=catalog;codec.placement=placement;
            var conn=go.AddUdonSharpComponent<BreadboardConnectivity>();conn.state=state;conn.layout=layout;
            var adapter=go.AddUdonSharpComponent<BreadboardMnaAdapter>();adapter.state=state;adapter.layout=layout;adapter.catalog=catalog;adapter.connectivity=conn;
            Check(state.Add(6,0,2,0,catalog.DefaultValue(6),-1),"Add Button");
            Check(state.pin1[0]==2 && state.values[0]==100000000f,"2-pitch footprint / default");
            string released=codec.Encode();Check(codec.TryDecode(released)&&codec.DecodedMatchesState(),"Released JSON");
            var off=adapter.BuildNetlist()[1].DataList;Check(off[0].String=="R_c000001" && off[2].DataList[0].Float==100000000f,"Off MNA");
            Check(state.ChangeParameter(0,.05f,-1),"Press");string pressed=codec.Encode();
            Check(codec.TryDecode(pressed)&&codec.DecodedMatchesState(),"Pressed JSON");
            var on=adapter.BuildNetlist()[1].DataList;Check(on[0].String==off[0].String && on[1].DataList[0].String==off[1].DataList[0].String && on[2].DataList[0].Float==.05f,"Stable resistor / nets");
            Check(!catalog.ValidParameters(6,1,-1,0),"Reject arbitrary switch value");
            Check(codec.TryDecode(released),"Decode release");codec.ApplyDecoded();Check(state.values[0]==100000000f,"Apply release");
            view=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BreadBoard/Prefabs/Parts/Button.prefab"));var part=view.GetComponent<BreadboardButtonPart>();
            part.InitializePart(layout,catalog,placement,1,false,null,null);
            for(int direction=0;direction<4;direction++) {
                part.ApplyState(6,0,direction,0,.05f,-1,true);Check(part.buttonRenderer.GetBlendShapeWeight(0)==100,"Push shape");
                part.ApplyState(6,0,direction,0,100000000f,-1,true);Check(part.buttonRenderer.GetBlendShapeWeight(0)==0,"Release shape");
            }
            part.isPreview=true;part.ApplyState(6,0,0,0,.05f,-1,true);Check(!part.interactCube.enabled && part.buttonRenderer.GetBlendShapeWeight(0)==0,"Preview cannot interact");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BreadBoard/model/Breadboard.fbx").transform.Find("Button");
            var a=source.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices;var b=part.buttonRenderer.sharedMesh.vertices;
            for(int i=0;i<a.Length;i++)Check(Mathf.Abs(a[i].y+source.localPosition.y-b[i].y)<.000001f,"Original height");
            Debug.Log("Button verification PASSED: "+checks+" assertions.");
        } finally {if(view)Object.DestroyImmediate(view);Object.DestroyImmediate(go);}
    }
}
