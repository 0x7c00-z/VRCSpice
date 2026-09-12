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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            Texture2D _IndexMat;
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

            float frag (v2f i) : SV_Target
            {
                uint2 dstTexcoord = (uint2)(i.uv * float2(_IndexMat_TexelSize.x, _DstTexHeight));
                if((int)dstTexcoord.y < _DstMatSize + 3){
                    return 0;
                }
                int srcCol = (int)asuint(_IndexMat[uint2(dstTexcoord.x, 0)]);
                if(srcCol < 0){
                    return 0;
                }
                return _MainTex[uint2(srcCol, dstTexcoord.y - _DstMatSize + _SrcMatSize)];
            }
            ENDCG
        }
    }
}
