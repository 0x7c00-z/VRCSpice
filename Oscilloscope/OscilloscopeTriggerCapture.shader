Shader "VRCSpice/Oscilloscope Trigger Capture"
{
    Properties
    {
        _MainTex ("Previous capture", 2D) = "black" {}
        _StateDump ("Current trigger state", 2D) = "black" {}
        _PreviousState ("Previous trigger state", 2D) = "black" {}
        _PreDump ("Previous pre-trigger samples", 2D) = "black" {}
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
            Texture2D<uint> _PreDump;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            uint StateLoad(uint x){return _StateDump.Load(int3(x,0,0));}
            uint CaptureLoad(uint x,uint y){return _MainTex.Load(int3(x,y,0));}
            uint PreLoad(uint x,uint y){return _PreDump.Load(int3(x,y,0));}

            uint CandidateColumn(uint column,uint ordinal)
            {
                uint timeValue,value1,value2,validBits;
                OscLoadCandidate(StateLoad(OSC_STATE_CANDIDATE_START),ordinal,max(1u,_Stride),
                    StateLoad(OSC_STATE_SEQUENCE),timeValue,value1,value2,validBits);
                if(column==0u)return timeValue;
                if(column==1u)return value1;
                if(column==2u)return value2;
                return validBits;
            }

            uint frag(v2f input):SV_Target
            {
                uint2 pixel=(uint2)input.vertex.xy;
                if(pixel.x>=4u || pixel.y>=OSC_CAPTURE_CAPACITY)return 0u;
                uint flags=StateLoad(OSC_STATE_FLAGS);
                if((flags&OSC_FLAG_CLEAR_CAPTURE)!=0u)return 0u;

                bool triggerStarted=(flags&OSC_FLAG_TRIGGER_STARTED)!=0u;
                uint appendCount=StateLoad(OSC_STATE_APPEND_COUNT);
                uint appendStart=StateLoad(OSC_STATE_APPEND_START);
                uint oldPost=StateLoad(OSC_STATE_OLD_POST_COUNT);
                if(triggerStarted)
                {
                    uint triggerOrdinal=StateLoad(OSC_STATE_TRIGGER_ORDINAL)-1u;
                    if(pixel.y<OSC_PRE_CAPACITY)
                    {
                        uint history=OSC_PRE_CAPACITY-1u-pixel.y;
                        if(history<triggerOrdinal)
                        {
                            uint ordinal=triggerOrdinal-1u-history;
                            return CandidateColumn(pixel.x,ordinal);
                        }
                        uint oldHistory=history-triggerOrdinal;
                        return PreLoad(pixel.x,oldHistory);
                    }
                    uint postSlot=pixel.y-OSC_PRE_CAPACITY;
                    if(postSlot<appendCount)return CandidateColumn(pixel.x,appendStart+postSlot);
                    return 0u;
                }

                if(pixel.y<OSC_PRE_CAPACITY+oldPost)return CaptureLoad(pixel.x,pixel.y);
                uint appendSlot=pixel.y-(OSC_PRE_CAPACITY+oldPost);
                if(appendSlot<appendCount)return CandidateColumn(pixel.x,appendStart+appendSlot);
                return 0u;
            }
            ENDCG
        }
    }
}
