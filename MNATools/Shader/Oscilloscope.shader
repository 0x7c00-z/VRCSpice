Shader "Unlit/Oscilloscope"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Row ("Row", Integer) = 0
        _YScale ("YScale", Float) = 1.0
        _MainColor ("MainColor", Color) = (1,1,1,1)
        _Time_Scale ("Time_Scale", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag


            #define LEN 100

            #include "UnityCG.cginc"

            #include "./Solver.hlsl"

            uint _Row;
            float _YScale;
            float4 _MainColor;
            float _Time_Scale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g{
                float4 vertex : POSITION;
                uint vid : TEXCOORD0;
            };

            v2g vert (appdata v, uint vid : SV_VertexID)
            {
                v2g o;
                o.vertex = v.vertex;
                o.vid = vid;
                return o;
            }

            struct g2f{
                float4 vertex : POSITION;
            };

            [maxvertexcount(LEN)]
            void geom(point v2g input[1], uint ind : SV_PrimitiveID, inout LineStream<g2f> stream)
            {
                g2f o;
                o.vertex = float4(0, 0, 0, 1);
                float time = 0;
                for(uint i=0;i<LEN;i++){
                    o.vertex = UnityObjectToClipPos(float4(0.5+time/_Time_Scale, get_data(i, _Row) * _YScale, 0, 1));
                    time = time - get_data_i_data_im1_timestep(i);
                    stream.Append(o);
                }
                stream.RestartStrip();
            }

            fixed4 frag (g2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = _MainColor;
                return col;
            }
            ENDCG
        }
    }
}
