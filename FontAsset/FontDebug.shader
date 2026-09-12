Shader "Unlit/FontDebug"
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
        Cull off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment fontfrag


            #include "UnityCG.cginc"
            #include "FontAsset.cginc"
            
            #define LINE 0.125
            #define CHWIDTH 0.125
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g{
                float4 vertex : POSITION;
                uint vid : TEXCOORD0;
            };

            Texture2D _MainTex;
            float4 _MainTex_TexelSize;

            v2g vert (appdata v, uint vid : SV_VertexID)
            {
                v2g o;
                o.vertex = v.vertex;
                o.vid = vid;
                return o;
            }

            [maxvertexcount(48)]
            void geom(point v2g input[1], uint ind : SV_PrimitiveID, inout TriangleStream<fontv2f> stream)
            {
                //uint ind = input[0].vid;
                float2 center = float2(-1, -1);
                if(ind<64){
                    [unroll]
                    for(int i=0;i<8;i++){
                        putChar(center + float2(CHWIDTH*(float)(i + 8*(ind%2)), LINE*(float)(ind/2)), LINE , 0,ind*8 + i, stream);
                    }
                }
                
                
            }
            
            
            ENDCG
        }
    }
}
