Shader "VRCSpice/Oscilloscope Trigger Pre"
{
    Properties
    {
        _MainTex ("Previous pre-trigger samples", 2D) = "black" {}
        _StateDump ("Current trigger state", 2D) = "black" {}
        _PreviousState ("Previous trigger state", 2D) = "black" {}
        _SolverDump ("Solver output dump", 2D) = "black" {}
        _Row1 ("CH1 row", Integer) = -1
        _Row2 ("CH2 row", Integer) = -1
        _Stride ("Time stride", Integer) = 10
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
            #include "OscilloscopeTriggerCommon.cginc"

            Texture2D<uint> _MainTex;
            Texture2D<uint> _StateDump;
            Texture2D<uint> _PreviousState;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            uint StateLoad(uint x){return _StateDump.Load(int3(x,0,0));}
            uint PreviousPreLoad(uint x,uint y){return _MainTex.Load(int3(x,y,0));}

            uint frag(v2f input):SV_Target
            {
                uint2 pixel=(uint2)input.vertex.xy;
                if(pixel.x>=4u || pixel.y>=OSC_PRE_CAPACITY)return 0u;
                uint candidateCount=StateLoad(OSC_STATE_CANDIDATE_COUNT);
                uint inserted=min(OSC_PRE_CAPACITY,candidateCount);
                uint flags=StateLoad(OSC_STATE_FLAGS);
                uint oldPreCount=StateLoad(OSC_STATE_OLD_PRE_COUNT);
                bool reset=(flags&OSC_FLAG_RESET_PRE)!=0u;
                uint resultCount=min(OSC_PRE_CAPACITY,inserted+(reset?0u:oldPreCount));
                if(pixel.y>=resultCount)return 0u;
                if(pixel.y>=inserted)
                {
                    uint oldHistory=pixel.y-inserted;
                    return oldHistory<oldPreCount?PreviousPreLoad(pixel.x,oldHistory):0u;
                }

                uint ordinal=candidateCount-1u-pixel.y;
                uint timeValue,value1,value2,validBits;
                OscLoadCandidate(StateLoad(OSC_STATE_CANDIDATE_START),ordinal,max(1u,_Stride),
                    StateLoad(OSC_STATE_SEQUENCE),timeValue,value1,value2,validBits);
                if(pixel.x==0u)return timeValue;
                if(pixel.x==1u)return value1;
                if(pixel.x==2u)return value2;
                return validBits;
            }
            ENDCG
        }
    }
}
