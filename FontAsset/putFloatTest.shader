Shader "Unlit/putFloatTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FontTex ("Font Asset", 2D) = "white" {}
        _FontColor ("Font Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom;
            #pragma fragment fontfrag

            #include "UnityCG.cginc"
            #include "../MNATools/Shader/Solver.hlsl"
            #include "FontAsset.cginc"

            #define LINE 0.3

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g
            {
                float4 vertex : SV_POSITION;
            };


            v2g vert (appdata v)
            {
                v2g o;
                o.vertex = v.vertex;
                return o;
            }

            [maxvertexcount(146)]
            void geom(point v2g input[1], uint ind : SV_PrimitiveID, inout TriangleStream<fontv2f> stream)
            {
                //uint ind = input[0].vid;
                float2 center = float2(0, 0);
                float2 cursur = 0;
                uint state = asuint(_MainTex[uint2(0, 0)]);
                //float val = _MainTex[uint2(0, 4)];

                float data = 0;
                data = calc_normalized_error();

                putFloatChar(center + cursur, LINE, 0, asuint(_MainTex[OFFSET_SOLVER_STATE].r), 20, stream);
                cursur.y -= LINE;
                putFloatChar(center + cursur, LINE, 0, data, 20, stream);
            }
            ENDCG
        }
    }
}
