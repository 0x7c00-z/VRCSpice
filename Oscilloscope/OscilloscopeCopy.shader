Shader "VRCSpice/Oscilloscope Copy"
{
    Properties
    {
        _MainTex ("Previous display dump", 2D) = "black" {}
        _StateDump ("Current trigger state", 2D) = "black" {}
        _CaptureDump ("Completed trigger capture", 2D) = "black" {}
        _SolverDump ("Solver output dump", 2D) = "black" {}
        _Row1 ("CH1 row", Integer) = -1
        _Row2 ("CH2 row", Integer) = -1
        _Stride ("Time stride", Integer) = 10
        _SamplingRevision ("Sampling revision", Integer) = 1
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
            Texture2D<uint> _CaptureDump;
            uint _SamplingRevision;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            uint DisplayLoad(uint x,uint y){return _MainTex.Load(int3(x,y,0));}
            uint StateLoad(uint x){return _StateDump.Load(int3(x,0,0));}
            uint CaptureLoad(uint x,uint y){return _CaptureDump.Load(int3(x,y,0));}

            uint ContinuousDisplay(uint2 pixel)
            {
                uint rawCount=min(OSC_RAW_CAPACITY,OscSolverLoad(2u,0u));
                uint sequence=OscSolverLoad(3u,0u);
                uint generation=OscSolverLoad(6u,0u);
                uint oldCount=min(OSC_CAPTURE_CAPACITY,DisplayLoad(0u,0u));
                uint oldSequence=DisplayLoad(1u,0u);
                uint oldGeneration=DisplayLoad(2u,0u);
                uint oldConfig=DisplayLoad(3u,0u);
                uint stride=max(1u,_Stride);
                bool generationChanged=generation!=oldGeneration;
                bool overrun=!generationChanged && sequence>oldSequence && sequence-oldSequence>rawCount;
                bool rebuild=_SamplingRevision!=oldConfig || generationChanged || sequence<oldSequence || overrun ||
                    (StateLoad(OSC_STATE_FLAGS)&OSC_FLAG_RESET_PRE)!=0u;
                uint oldest=rawCount==0u?sequence+1u:sequence-rawCount+1u;
                uint start=rebuild?oldest:oldSequence+1u;
                uint candidates=OscCandidateCount(start,sequence,stride);
                uint inserted=min(OSC_CAPTURE_CAPACITY,candidates);
                uint resultCount=min(OSC_CAPTURE_CAPACITY,inserted+(rebuild?0u:oldCount));

                if(pixel.y==0u)
                {
                    if(pixel.x==0u)return resultCount;
                    if(pixel.x==1u)return sequence;
                    if(pixel.x==2u)return generation;
                    if(pixel.x==3u)return _SamplingRevision;
                    return 0u;
                }
                uint history=pixel.y-1u;
                if(history>=resultCount || history>=OSC_CAPTURE_CAPACITY)return 0u;
                if(history>=inserted)
                {
                    uint oldHistory=history-inserted;
                    return oldHistory<oldCount?DisplayLoad(pixel.x,oldHistory+1u):0u;
                }
                uint lastAligned=sequence-sequence%stride;
                uint sampleSequence=lastAligned-history*stride;
                uint sourceY=sequence-sampleSequence+1u;
                uint dataN=OscSolverLoad(1u,0u);
                if(pixel.x==0u)return OscSolverLoad(dataN,sourceY);
                uint valid1,valid2;
                uint value1=OscChannelValue(_Row1,dataN,sourceY,valid1);
                uint value2=OscChannelValue(_Row2,dataN,sourceY,valid2);
                if(pixel.x==1u)return value1;
                if(pixel.x==2u)return value2;
                return valid1|(valid2<<1u);
            }

            uint frag(v2f input):SV_Target
            {
                uint2 pixel=(uint2)input.vertex.xy;
                if(pixel.x>=4u || pixel.y>OSC_CAPTURE_CAPACITY)return 0u;
                if(StateLoad(OSC_STATE_ENABLED)==0u)return ContinuousDisplay(pixel);

                uint flags=StateLoad(OSC_STATE_FLAGS);
                uint acquisition=StateLoad(OSC_STATE_ACQUISITION);
                uint hasTriggerDisplay=StateLoad(OSC_STATE_HAS_TRIGGER_DISPLAY);
                if((flags&OSC_FLAG_CLEAR_DISPLAY)!=0u)
                {
                    // A new sampling series may already contain useful points.  While
                    // the first trigger is only preparing/waiting, rebuild the live
                    // display from that new series instead of flashing an empty trace.
                    if(hasTriggerDisplay==0u &&
                        (acquisition==OSC_PREPARING || acquisition==OSC_WAITING))
                        return ContinuousDisplay(pixel);
                    if(pixel.y==0u)
                    {
                        if(pixel.x==1u)return StateLoad(OSC_STATE_SEQUENCE);
                        if(pixel.x==2u)return StateLoad(OSC_STATE_GENERATION);
                        if(pixel.x==3u)return StateLoad(OSC_STATE_SAMPLING_REVISION);
                    }
                    return 0u;
                }
                if((flags&OSC_FLAG_CAPTURE_COMPLETE)==0u)
                {
                    if(hasTriggerDisplay==0u &&
                        (acquisition==OSC_PREPARING || acquisition==OSC_WAITING))
                        return ContinuousDisplay(pixel);
                    return DisplayLoad(pixel.x,pixel.y);
                }

                if(pixel.y==0u)
                {
                    if(pixel.x==0u)return OSC_CAPTURE_CAPACITY;
                    if(pixel.x==1u)return StateLoad(OSC_STATE_SEQUENCE);
                    if(pixel.x==2u)return StateLoad(OSC_STATE_GENERATION);
                    if(pixel.x==3u)return StateLoad(OSC_STATE_SAMPLING_REVISION);
                    return 0u;
                }
                uint history=pixel.y-1u;
                uint chronological=OSC_CAPTURE_CAPACITY-1u-history;
                return CaptureLoad(pixel.x,chronological);
            }
            ENDCG
        }
    }
}
