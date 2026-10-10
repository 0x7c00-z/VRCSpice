Shader "Unlit/CopyColumn"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _IndexMat ("Column map", 2D) = "black" {}
        _SrcMatSize ("Source MNA size", Integer) = 0
        _DstMatSize ("Destination MNA size", Integer) = 0
        _DstTexHeight ("Destination height", Integer) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            Texture2D<uint> _MainTex;
            Texture2D<float2> _IndexMat;
            float4 _MainTex_TexelSize;
            float4 _IndexMat_TexelSize;
            uint _SrcMatSize;
            uint _DstMatSize;
            uint _DstTexHeight;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}

            uint LoadSourceColumn(uint column)
            {
                uint2 parts=(uint2)round(_IndexMat.Load(int3(column,0,0))*65535.0);
                return parts.x|(parts.y<<16);
            }

            uint frag(v2f input):SV_Target
            {
                uint2 dst=(uint2)(input.uv*float2(_IndexMat_TexelSize.z,_DstTexHeight));
                if(_SrcMatSize==0u)return 0u;

                // Preserve the completed output count and its absolute time.
                if(dst.y==0u && (dst.x==7u || dst.x==10u || dst.x==12u || dst.x==13u))
                    return _MainTex.Load(int3(dst,0));

                uint dstOutput=4u+3u*_DstMatSize;
                uint srcOutput=4u+3u*_SrcMatSize;
                if(dst.y>=dstOutput && dst.y<dstOutput+1000u && dst.x<=_DstMatSize)
                {
                    uint sourceColumn=LoadSourceColumn(dst.x);
                    bool valid=dst.x==_DstMatSize?sourceColumn==_SrcMatSize:sourceColumn<_SrcMatSize;
                    if(valid)
                    {
                        uint2 src=uint2(sourceColumn,srcOutput+dst.y-dstOutput);
                        if(src.x<(uint)_MainTex_TexelSize.z && src.y<(uint)_MainTex_TexelSize.w)
                            return _MainTex.Load(int3(src,0));
                    }
                }
                return 0u;
            }
            ENDCG
        }
    }
}
