Shader "Breadboard/Color"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float shade : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.shade=0.65+0.35*abs(dot(UnityObjectToWorldNormal(v.normal),normalize(float3(0.3,1,0.4)))); return o; }
            fixed4 frag(v2f i) : SV_Target { return fixed4(_Color.rgb*i.shade,1); }
            ENDCG
        }
    }
}
