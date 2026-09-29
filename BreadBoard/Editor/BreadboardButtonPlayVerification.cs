using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.SDKBase;

public static class BreadboardButtonPlayVerification
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new System.Exception("Button Udon: "+message);checks++;}
    static UdonBehaviour Back(UdonSharp.UdonSharpBehaviour b){return UdonSharpEditorUtility.GetBackingUdonBehaviour(b);}
    static void Input(UdonBehaviour b,bool value,HandType hand){b.SetProgramVariable("inputUseBoolValue",value);b.SetProgramVariable("inputUseArgs",new UdonInputEventArgs(value,hand));b.SendCustomEvent("_inputUse");}
    static void Request(UdonBehaviour u,int id,bool held,int gesture){u.SetProgramVariable("__0_id__param",id);u.SetProgramVariable("__0_held__param",held);u.SetProgramVariable("__0_gesture__param",gesture);u.SendCustomEvent("__0_RequestButton");}
    [MenuItem("Tools/Breadboard/Verify button input (Play mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new System.InvalidOperationException("Enter Play mode");checks=0;
        BreadboardDynamicPartVerification.Run();
        var sync=Object.FindObjectOfType<BreadboardSync>();var u=Back(sync);var r=sync.controller.boardRenderer;
        var p=r.partsRoot.GetComponentInChildren<BreadboardButtonPart>(true);var b=Back(p);var s=Back(sync.state);
        int player=Networking.LocalPlayer.playerId;
        u.SetProgramVariable("hasState",true);u.SetProgramVariable("awaitingGrant",false);u.SetProgramVariable("pendingGrant",player);u.SetProgramVariable("acceptedGrant",player);
        var values=(float[])s.GetProgramVariable("values");int slot=(int)s.GetProgramVariable("count")-1;int id=((int[])s.GetProgramVariable("ids"))[slot];
        Input(b,true,HandType.RIGHT);b.SendCustomEvent("_interact");
        Check(values[slot]==.05f && p.buttonRenderer.GetBlendShapeWeight(0)==100,"press updates resistance and mesh");
        Input(b,false,HandType.LEFT);Check(values[slot]==.05f,"other hand cannot release");
        Input(b,false,HandType.RIGHT);Check(values[slot]==100000000f && p.buttonRenderer.GetBlendShapeWeight(0)==0,"release updates resistance and mesh");
        Request(u,id,true,1);Check(values[slot]==100000000f,"late hold after release ignored");
        Request(u,id,true,2);Check(values[slot]==.05f,"new gesture accepted");
        Request(u,id,false,1);Check(values[slot]==.05f,"old release cannot cancel new press");
        int revision=(int)s.GetProgramVariable("revision");Request(u,id,true,2);Check((int)s.GetProgramVariable("revision")==revision,"heartbeat does not rebuild circuit");
        u.SetProgramVariable("buttonExpiry",new float[sync.state.capacity]);u.SendCustomEvent("_update");
        Check(values[slot]==100000000f,"expired hold released");
        u.SendCustomEvent("_onPreSerialization");var snapshot=(string)u.GetProgramVariable("snapshot");Check(snapshot.Contains("Button") && snapshot.Contains("valueSI"),"latest button state included in shared snapshot");
        s.SetProgramVariable("__0_slot__param",slot);s.SendCustomEvent("__0_Remove");Back(r).SendCustomEvent("Rebuild");Check(!p.gameObject.activeSelf,"deleted button hidden");
        revision=(int)s.GetProgramVariable("revision");Request(u,id,true,3);Check((int)s.GetProgramVariable("revision")==revision,"request for deleted ID ignored");
        Debug.Log("Button Udon verification PASSED: "+checks+" assertions.");
    }
}
