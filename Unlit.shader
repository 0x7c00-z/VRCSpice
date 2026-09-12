Shader "Unlit/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            Texture2D _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                uint w, h = 0;
                _MainTex.GetDimensions(w, h);
                float data = _MainTex[uint2(w*i.uv.x, h*i.uv.y)];
                return fixed4(max(data, 0), (frac((float)w*i.uv.x) > 0.8 || frac((float)h*i.uv.y) > 0.8) ? 1 : 0, max(-data, 0), 1);
            }
            ENDCG
        }
    }
}
