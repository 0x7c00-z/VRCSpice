Shader "Unlit/Processor"
{
    Properties
    {
        _DATA_N ("The number of datas", Integer) = 1 //size of the vector to be solved

        //Inputs
        _A ("A", 2D) = "black" {}
        _B ("B", 2D) = "black" {}
        _C ("C", 2D) = "black" {}
        _Is ("Is", 2D) = "black" {}
        _rhs ("rhs", 2D) = "black" {}
        _DeltaTime ("Delta time", Float) = 0.01
        _MainTex ("Texture", 2D) = "black" {}
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

            float frag (v2f i) : SV_Target
            {
                //return _MainTex[uv2texel(i.uv)].r; //DEBUG
                return process(uv2texel(i.uv));
            }
            ENDCG
        }
    }
}
