using UnityEngine;
using UnityEditor;
using System.IO;

public static class BreadboardWireVerification
{
    [MenuItem("Tools/Breadboard/Verify modeled wire")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Run outside Play mode.");
        var r=Object.FindObjectOfType<BreadboardRenderer>();
        var go=Object.Instantiate(r.catalog.partPrefabs[0]);var p=go.GetComponent<BreadboardWirePart>();
        var baked=new Mesh();int checks=0;
        try {
            p.InitializePart(r.layout,r.catalog,r.placement,99,false,r.previewValid,r.previewInvalid);
            for(int length=1;length<=9;length++)for(int orientation=0;orientation<4;orientation++) {
                p.ApplyState(0,0,orientation,length,0,-1,true);go.SetActive(true);p.wireRenderer.BakeMesh(baked);
                Vector3 a=Vector3.zero,b=Vector3.zero;int na=0,nb=0;
                foreach(var v in baked.vertices) {
                    if(Mathf.Abs(v.y+.025f)>.00001f)continue;
                    if(v.x<.005f){a+=v;na++;}else{b+=v;nb++;}
                }
                a/=na;b/=nb;
                // Compare hole positions at the inserted pin-tip depth.
                Vector3 depth = Vector3.down * (.025f * r.layout.pitch / .01f);
                a=p.wireRenderer.transform.TransformPoint(a);b=p.wireRenderer.transform.TransformPoint(b);
                if(Vector3.Distance(a,r.layout.holePositions[0]+depth)>.00001f || Vector3.Distance(b,r.layout.holePositions[0]+depth+r.layout.Direction(orientation)*length*r.layout.pitch)>.00001f)throw new System.Exception("Pin positions "+length+" / "+orientation);
                if(p.wireRenderer.sharedMaterials[p.colorSlot]!=p.lengthMaterials[length-1])throw new System.Exception("Color");checks+=2;
            }
            p.ApplyState(0,0,0,12,0,-1,true);
            if(p.wireRenderer.gameObject.activeSelf || !p.legacyLine.gameObject.activeSelf || p.legacyLine.GetPosition(3)!=r.layout.holePositions[0]+r.layout.Direction(0)*r.layout.pitch*12)throw new System.Exception("Legacy");checks++;
            p.ApplyState(0,0,0,2,0,-1,true);
            if(!p.wireRenderer.gameObject.activeSelf || p.legacyLine.gameObject.activeSelf)throw new System.Exception("Restore");checks++;
            p.isPreview=true;
            foreach(bool valid in new[]{false,true}) {
                p.ApplyState(0,0,0,9,0,-1,valid);
                foreach(var m in p.wireRenderer.sharedMaterials)if(m!=(valid?r.previewValid:r.previewInvalid))throw new System.Exception("Ghost");checks++;
            }
            Debug.Log("Modeled wire verification PASSED: "+checks+" assertions.");
        } finally {Object.DestroyImmediate(go);Object.DestroyImmediate(baked);}
    }
    public static void RenderExamples()
    {
        var preview=new PreviewRenderUtility();
        try {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BreadBoard/Prefabs/Parts/Wire.prefab");
            for(int length=1;length<=9;length++) {
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);go.SetActive(true);
                go.transform.position=new Vector3(0,0,(length-1)*.012f);
                var p=go.GetComponent<BreadboardWirePart>();p.wireRenderer.SetBlendShapeWeight(0,(length-1)*100f/9);
                var mats=new Material[2];mats[p.metalSlot]=p.metalMaterial;mats[p.colorSlot]=p.lengthMaterials[length-1];p.wireRenderer.sharedMaterials=mats;
            }
            preview.camera.transform.position=new Vector3(.13f,.18f,-.1f);preview.camera.transform.LookAt(new Vector3(.045f,.012f,.048f));
            preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=2;preview.camera.orthographic=true;preview.camera.orthographicSize=.085f;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.17f);
            preview.BeginStaticPreview(new Rect(0,0,800,800));preview.Render();var image=preview.EndStaticPreview();
            Directory.CreateDirectory("Temp/WireUpgrade");File.WriteAllBytes("Temp/WireUpgrade/examples.png",image.EncodeToPNG());Object.DestroyImmediate(image);
        } finally {preview.Cleanup();}
    }
}
