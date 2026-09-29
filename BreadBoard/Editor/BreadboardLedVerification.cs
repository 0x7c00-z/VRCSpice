using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using UnityEngine.Experimental.Rendering;
using System.IO;

public static class BreadboardLedVerification
{
    static void Check(bool ok,string message){if(!ok)throw new System.Exception("LED: "+message);}
    public static RenderTexture Buffer()
    {
        var desc=new RenderTextureDescriptor(32,32);desc.graphicsFormat=GraphicsFormat.R32_UInt;desc.depthBufferBits=0;desc.msaaSamples=1;
        var tex=new RenderTexture(desc);tex.Create();
        var writer=new Material(Shader.Find("Hidden/Breadboard/LED Test Buffer"));
        Graphics.Blit(null,tex,writer);Object.DestroyImmediate(writer);return tex;
    }
    [MenuItem("Tools/Breadboard/Verify LED")]
    public static void Run()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play");
        var r=Object.FindObjectOfType<BreadboardRenderer>();var catalog=r.catalog;
        Check(catalog.kinds[7]=="LED" && catalog.Span(7,0)==1,"catalog and footprint");
        int model=catalog.DefaultModel(7);Check(model>=0 && catalog.ValidParameters(7,0,model,0),"model");
        Check(catalog.MnaConstants(7,0,model).Count==7,"seven diode constants");
        var setup=new GameObject("LED test");var preview=new PreviewRenderUtility();Material material=null;RenderTexture buffer=null;RenderTexture target=null;Texture2D read=null;
        try {
            var layout=setup.AddUdonSharpComponent<BreadboardLayout>();BreadboardBuilder.ConfigureLayout(layout);
            var place=setup.AddUdonSharpComponent<BreadboardPlacement>();place.layout=layout;place.catalog=catalog;
            var state=setup.AddUdonSharpComponent<BreadboardState>();state.layout=layout;state.catalog=catalog;state.placement=place;
            var codec=setup.AddUdonSharpComponent<BreadboardCodec>();codec.state=state;codec.layout=layout;codec.catalog=catalog;codec.placement=place;
            var conn=setup.AddUdonSharpComponent<BreadboardConnectivity>();conn.state=state;conn.layout=layout;
            var adapter=setup.AddUdonSharpComponent<BreadboardMnaAdapter>();adapter.state=state;adapter.layout=layout;adapter.catalog=catalog;adapter.connectivity=conn;
            Check(state.Add(7,0,2,0,0,model),"place LED");var json=codec.Encode();Check(codec.TryDecode(json)&&codec.DecodedMatchesState(),"JSON round trip");
            var record=adapter.BuildNetlist()[1].DataList;Check(record[0].String=="D_c000001" && record[2].DataList.Count==7,"MNA diode name");
            buffer=Buffer();material=new Material(Shader.Find("Breadboard/LED Current Emission"));material.SetTexture("_MainTex",buffer);material.SetInteger("_DATA_N",8);
            target=new RenderTexture(16,16,0,RenderTextureFormat.ARGBFloat);target.Create();read=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
            float[] actual=new float[4];int[] rows={2,5,6,-1};
            for(int i=0;i<4;i++) {
                var old=RenderTexture.active;RenderTexture.active=target;GL.Clear(true,true,Color.clear);RenderTexture.active=old;
                material.SetInteger("_CurrentRow",rows[i]);Graphics.Blit(buffer,target,material);
                RenderTexture.active=target;read.ReadPixels(new Rect(0,0,16,16),0,0);read.Apply();actual[i]=read.GetPixel(8,8).r;RenderTexture.active=old;
            }
            Check(Mathf.Abs(actual[0]-actual[3]-.5f)<.01f && Mathf.Abs(actual[1]-actual[3]-2f)<.01f,"current proportional HDR output");
            Check(actual[3]>0 && Mathf.Abs(actual[2]-actual[3])<.0001f,"negative and missing current retain base color");
            material.SetInteger("_CurrentRow",-1);
            for(int i=0;i<3;i++) {
                var go=Object.Instantiate(catalog.partPrefabs[7]);preview.AddSingleGO(go);go.SetActive(true);go.transform.position=new Vector3(i*.04f,0,0);
                var part=go.GetComponent<BreadboardLedPart>();var mats=part.ledRenderer.sharedMaterials;mats[part.emitterSlot]=material;part.ledRenderer.sharedMaterials=mats;
                var block=new MaterialPropertyBlock();block.SetInteger("_CurrentRow",i==0?-1:i==1?2:5);part.ledRenderer.SetPropertyBlock(block);
            }
            preview.camera.transform.position=new Vector3(.075f,.1f,-.17f);preview.camera.transform.LookAt(new Vector3(.045f,.02f,0));preview.camera.orthographic=true;preview.camera.orthographicSize=.065f;
            preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=2;preview.camera.backgroundColor=new Color(.1f,.12f,.14f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.BeginStaticPreview(new Rect(0,0,900,600));preview.Render();var image=preview.EndStaticPreview();Directory.CreateDirectory("Temp/LedUpgrade");File.WriteAllBytes("Temp/LedUpgrade/examples.png",image.EncodeToPNG());Object.DestroyImmediate(image);
            Debug.Log("LED verification PASSED: catalog, JSON, MNA, GPU current values 0/5/20 mA, negative current, invalid row.");
        }finally{preview.Cleanup();Object.DestroyImmediate(setup);if(material)Object.DestroyImmediate(material);if(buffer){buffer.Release();Object.DestroyImmediate(buffer);}if(target){target.Release();Object.DestroyImmediate(target);}if(read)Object.DestroyImmediate(read);}
    }
}
