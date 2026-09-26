using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using VRC.Udon;

// Runs the compiled Udon programs, including virtual dispatch on instantiated prefabs.
public static class BreadboardDynamicPartVerification
{
    static int checks;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new System.Exception("Dynamic part check failed: " + message);
        checks++;
    }
    static UdonBehaviour Back(UdonSharp.UdonSharpBehaviour b) { return UdonSharpEditorUtility.GetBackingUdonBehaviour(b); }
    static void Add(UdonBehaviour s, int kind, int anchor)
    {
        s.SetProgramVariable("__1_kind__param",kind); s.SetProgramVariable("__1_anchor__param",anchor);
        s.SetProgramVariable("__1_orientation__param",2); s.SetProgramVariable("__1_length__param",2);
        s.SetProgramVariable("__0_value__param",kind == 2 ? 1e-7f : kind == 3 ? 1e-3f : 1000f);
        s.SetProgramVariable("__0_model__param",kind == 4 ? 0 : kind == 5 ? 2 : -1);
        s.SendCustomEvent("__0_Add"); Check((bool)s.GetProgramVariable("__0___0_Add__ret"),"add kind " + kind);
    }
    static void Preview(UdonBehaviour r, int kind, bool valid)
    {
        r.SetProgramVariable("__1_kind__param",kind); r.SetProgramVariable("__0_anchor__param",180);
        r.SetProgramVariable("__0_orientation__param",2); r.SetProgramVariable("__0_length__param",2);
        r.SetProgramVariable("__0_value__param",1000f); r.SetProgramVariable("__0_model__param",-1);
        r.SetProgramVariable("__0_valid__param",valid); r.SendCustomEvent("__0_Preview");
    }
    [MenuItem("Tools/Breadboard/Verify dynamic parts (Play mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new System.InvalidOperationException("Run in Play mode on a disposable test circuit.");
        checks = 0;
        var renderer = Object.FindObjectOfType<BreadboardRenderer>();
        // Isolate view tests from initial network snapshot application and hand input.
        var sync = Back(Object.FindObjectOfType<BreadboardSync>());
        sync.enabled = false;
        sync.SetProgramVariable("hasState",true);
        Back(Object.FindObjectOfType<BreadboardController>()).enabled = false;
        var s = Back(renderer.state); var r = Back(renderer);
        s.SendCustomEvent("Initialize");
        while ((int)s.GetProgramVariable("count") > 0)
        { s.SetProgramVariable("__0_slot__param",0); s.SendCustomEvent("__0_Remove"); }
        r.SendCustomEvent("Rebuild");
        for (int kind = 0; kind < 6; kind++) Add(s,kind,kind*30);
        r.SendCustomEvent("Rebuild");
        var ids = (int[])s.GetProgramVariable("ids");
        var objects = new GameObject[6];
        for (int kind = 0; kind < 6; kind++)
        {
            var t = renderer.partsRoot.Find("Part_" + ids[kind]); Check(t != null,"created kind " + kind);
            objects[kind] = t.gameObject;
            var part = t.GetComponent<BreadboardSimplePart>(); var u = Back(part);
            Check((bool)u.GetProgramVariable("initialized") && !(bool)u.GetProgramVariable("isPreview"),"base initialization " + kind);
            Check(part.label.text.StartsWith(renderer.catalog.kinds[kind] + ids[kind]),"derived virtual rendering " + kind);
            Check(t.GetComponentsInChildren<Collider>(true).Length == 0 && t.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true).Length == 0,"local nonphysical view " + kind);
        }
        r.SendCustomEvent("Rebuild");
        for (int kind = 0; kind < 6; kind++) Check(renderer.partsRoot.Find("Part_" + ids[kind]).gameObject == objects[kind],"duplicate rebuild reuses kind " + kind);
        s.SetProgramVariable("__0_slot__param",1); s.SendCustomEvent("__0_Remove"); r.SendCustomEvent("Rebuild");
        Check(!objects[1].activeSelf,"deleted part hidden before deferred destruction");
        Check(renderer.partsRoot.Find("Part_" + ids[1]).gameObject == objects[2],"array compaction preserves component ID instance");
        s.SetProgramVariable("__1_slot__param",1); s.SetProgramVariable("__1_value__param",2.2e-7f); s.SetProgramVariable("__1_model__param",-1);
        s.SendCustomEvent("__0_ChangeParameter"); r.SendCustomEvent("Rebuild");
        Check(objects[2].GetComponent<BreadboardSimplePart>().label.text.Contains("220"),"value update reaches existing view");
        int revision = (int)s.GetProgramVariable("revision"); int count = (int)s.GetProgramVariable("count");
        Preview(r,1,true);
        BreadboardSimplePart first = null;
        foreach(Transform t in renderer.partsRoot) if(t.name == "Preview" && t.gameObject.activeSelf) first = t.GetComponent<BreadboardSimplePart>();
        Check((bool)Back(first).GetProgramVariable("isPreview") && first.bodyRenderer.sharedMaterial == renderer.previewValid,"preview base flag and ghost material");
        Preview(r,1,false);
        Check(first.bodyRenderer.sharedMaterial == renderer.previewInvalid,"preview validity updates same instance");
        r.SendCustomEvent("HidePreview"); Check(!first.gameObject.activeSelf,"hide preview");
        Preview(r,1,true); Check(first.gameObject.activeSelf,"reuse hidden preview");
        Preview(r,5,true); Check(!first.gameObject.activeSelf,"kind change releases previous preview");
        int activePreviews = 0;
        foreach(Transform t in renderer.partsRoot) if(t.name == "Preview" && t.gameObject.activeSelf)
        { activePreviews++; Check(t.GetComponent<BreadboardSimplePart>().label.text.StartsWith("Q"),"replacement preview has new kind"); }
        Check(activePreviews == 1,"one active preview");
        Check((int)s.GetProgramVariable("revision") == revision && (int)s.GetProgramVariable("count") == count,"preview never edits circuit");
        r.SendCustomEvent("HidePreview");
        // Keep the test fixture consistent if ClientSim delivers an initial snapshot later.
        var codec = Back(Object.FindObjectOfType<BreadboardCodec>());
        codec.SendCustomEvent("Encode");
        var snapshot = codec.GetProgramVariable("__0_Encode__ret");
        sync.SetProgramVariable("snapshot",snapshot); sync.SetProgramVariable("pendingSnapshot",snapshot);
        Debug.Log("Dynamic part verification PASSED: " + checks + " assertions.");
    }
}
