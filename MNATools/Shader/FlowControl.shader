Shader "Unlit/FlowControl"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _DATA_N ("The number of datas", Integer) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Solver.hlsl"

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

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            uint frag (v2f i) : SV_Target
            {
                return flowControl(uv2texel(i.uv));
            }
            ENDCG
        }
    }
}
