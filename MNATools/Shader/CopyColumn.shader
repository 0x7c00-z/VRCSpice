Shader "Unlit/CopyColumn"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {} //Source
        _IndexMat ("Texture", 2D) = "white" {} //same width as Destination
        _SrcMatSize ("Source Matrix size", Integer) = 1
        _DstMatSize ("Destination Matrix size", Integer) = 1
        _DstTexHeight("Destination Texture Height", Integer) = 1
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
            #include "Solver_variables.hlsl"

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

            float4 _MainTex_ST;
            Texture2D<float> _IndexMat; //Numeric column indices; -1 means no previous column.
            float4 _IndexMat_TexelSize;
            int _SrcMatSize;
            int _DstMatSize;
            int _DstTexHeight;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            uint frag (v2f i) : SV_Target
            {
                uint2 dstTexcoord = (uint2)(i.uv * float2(_IndexMat_TexelSize.z, _DstTexHeight));
                if (_SrcMatSize == 0 || (int)dstTexcoord.y < _DstMatSize + 3)
                {
                    return SolverStoreUInt(0u);
                }
                int srcCol = (int)_IndexMat.Load(int3(dstTexcoord.x, 0, 0));
                int srcRow = (int)dstTexcoord.y - _DstMatSize + _SrcMatSize;
                if (srcCol < 0 || srcCol >= (int)_MainTex_TexelSize.z
                    || srcRow < 0 || srcRow >= (int)_MainTex_TexelSize.w)
                {
                    return SolverStoreUInt(0u);
                }
                return SolverLoadUInt(uint2(srcCol, srcRow));
            }
            ENDCG
        }
    }
}
