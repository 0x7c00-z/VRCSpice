Shader "VRCSpice/Solver Output Dump"
{
    Properties
    {
        _MainTex ("Solver state", 2D) = "black" {}
        _DATA_N ("MNA vector size", Integer) = 0
        _OutputDeltaTime ("Output interval", Float) = 0.00001
        _DumpGeneration ("Dump generation", Integer) = 1
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
            #include "Solver_variables.hlsl"

            uint _DumpGeneration;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}

            uint frag(v2f input):SV_Target
            {
                // The public dump can be smaller than the internal solver texture.
                // SV_POSITION addresses the destination; _MainTex_TexelSize addresses the source.
                uint2 pixel=(uint2)input.vertex.xy;
                uint count=min(OUTPUT_BUFFER_LENGTH,SolverLoadUInt(OFFSET_OUTPUT_COUNT));
                if(pixel.y==0u)
                {
                    if(pixel.x==0u)return 1u;
                    if(pixel.x==1u)return _DATA_N;
                    if(pixel.x==2u)return count;
                    if(pixel.x==3u)return SolverLoadUInt(OFFSET_OUTPUT_SEQUENCE);
                    if(pixel.x==4u)return asuint(_OutputDeltaTime);
                    if(pixel.x==5u)return SolverLoadUInt(OFFSET_LAST_OUTPUT_TIME);
                    if(pixel.x==6u)return _DumpGeneration;
                    return 0u;
                }
                uint history=pixel.y-1u;
                if(history>=count || history>=OUTPUT_BUFFER_LENGTH)return 0u;
                if(pixel.x<_DATA_N)return SolverLoadUInt(OFFSET_OUT_BUFFER+uint2(pixel.x,history));
                if(pixel.x==_DATA_N)
                {
                    float lastTime=SolverLoadFloat(OFFSET_LAST_OUTPUT_TIME);
                    return asuint(lastTime-(float)history*_OutputDeltaTime);
                }
                return 0u;
            }
            ENDCG
        }
    }
}
