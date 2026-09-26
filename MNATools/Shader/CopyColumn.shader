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

            Texture2D<float2> _IndexMat; //RG32 packed uint; 0xffffffff means no previous column.
            float4 _IndexMat_TexelSize;
            int _SrcMatSize;
            int _DstMatSize;
            int _DstTexHeight;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            uint LoadSourceColumn(uint column)
            {
                uint2 parts = (uint2)round(_IndexMat.Load(int3(column, 0, 0)) * 65535.0);
                return parts.x | (parts.y << 16);
            }

            uint frag (v2f i) : SV_Target
            {
                uint result = 0u;
                uint2 dstTexcoord = (uint2)(i.uv * float2(_IndexMat_TexelSize.z, _DstTexHeight));
                // Solver layout: header, xdot, predictor, N matrix rows, two work rows.
                // Only accepted history (N + 5 onward) survives a circuit change.
                // Reset the header and work rows for both first creation and restart.
                if (_SrcMatSize > 0 && (int)dstTexcoord.y >= _DstMatSize + 5
                    && dstTexcoord.x <= (uint)_DstMatSize)
                {
                    uint srcCol = LoadSourceColumn(dstTexcoord.x);
                    int srcRow = (int)dstTexcoord.y - _DstMatSize + _SrcMatSize;
                    bool validColumn = dstTexcoord.x == (uint)_DstMatSize
                        ? srcCol == (uint)_SrcMatSize : srcCol < (uint)_SrcMatSize;
                    if (validColumn && srcCol < (uint)_MainTex_TexelSize.z
                        && srcRow >= 0 && srcRow < (int)_MainTex_TexelSize.w)
                    {
                        result = SolverLoadUInt(uint2(srcCol, srcRow));
                    }
                }
                return result;
            }
            ENDCG
        }
    }
}
