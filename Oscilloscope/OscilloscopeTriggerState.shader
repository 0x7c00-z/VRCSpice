Shader "VRCSpice/Oscilloscope Trigger State"
{
    Properties
    {
        _MainTex ("Previous trigger state", 2D) = "black" {}
        _PreDump ("Previous pre-trigger samples", 2D) = "black" {}
        _SolverDump ("Solver output dump", 2D) = "black" {}
        _Row1 ("CH1 row", Integer) = -1
        _Row2 ("CH2 row", Integer) = -1
        _Stride ("Time stride", Integer) = 10
        _SamplingRevision ("Sampling revision", Integer) = 1
        _TriggerRevision ("Trigger revision", Integer) = 1
        _TriggerEnabled ("Trigger enabled", Integer) = 0
        _TriggerMode ("Trigger mode", Integer) = 0
        _TriggerSource ("Trigger source", Integer) = 0
        _TriggerFalling ("Falling edge", Integer) = 0
        _TriggerLevel ("Trigger level", Float) = 0
        _ForceReset ("Force reset", Integer) = 0
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
            Texture2D<uint> _PreDump;
            uint _SamplingRevision;
            uint _TriggerRevision;
            uint _TriggerEnabled;
            uint _TriggerMode;
            uint _TriggerSource;
            uint _TriggerFalling;
            float _TriggerLevel;
            uint _ForceReset;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; };
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            uint StateLoad(uint x){return _MainTex.Load(int3(x,0,0));}
            uint PreLoad(uint x,uint y){return _PreDump.Load(int3(x,y,0));}

            struct TriggerState
            {
                uint sequence;
                uint generation;
                uint samplingRevision;
                uint triggerRevision;
                uint acquisition;
                uint preCount;
                uint readyCount;
                uint previousValue;
                uint previousValid;
                uint postCount;
                uint autoWait;
                uint candidateStart;
                uint candidateCount;
                uint triggerOrdinal;
                uint appendStart;
                uint appendCount;
                uint flags;
                uint oldPostCount;
                uint oldPreCount;
                uint mode;
                uint enabled;
                uint hasTriggerDisplay;
            };

            void ReadReadyFromPre(uint preCount,uint source,out uint ready,out uint previousValue,out uint previousValid)
            {
                ready=0u;previousValue=asuint(0.0);previousValid=0u;
                [loop]for(uint history=0u;history<OSC_PRE_CAPACITY;history++)
                {
                    if(history>=preCount)break;
                    uint validBits=PreLoad(3u,history);
                    if(OscSelectedValid(validBits,source)==0u)break;
                    if(history==0u)
                    {
                        previousValue=PreLoad(source+1u,history);
                        previousValid=1u;
                    }
                    ready++;
                }
            }

            void ScanReadiness(uint firstSequence,uint candidateCount,uint stride,uint latestSequence,uint source,
                uint initialReady,uint initialPreviousValue,uint initialPreviousValid,
                out uint finalReady,out uint finalPreviousValue,out uint finalPreviousValid,
                out uint firstEligible,out uint firstCrossing)
            {
                uint ready=initialReady;
                uint previousValue=initialPreviousValue;
                uint previousValid=initialPreviousValid;
                firstEligible=0xffffffffu;
                firstCrossing=0xffffffffu;
                [loop]for(uint ordinal=0u;ordinal<OSC_RAW_CAPACITY;ordinal++)
                {
                    if(ordinal>=candidateCount)break;
                    uint timeValue,value1,value2,validBits;
                    OscLoadCandidate(firstSequence,ordinal,stride,latestSequence,timeValue,value1,value2,validBits);
                    uint valid=OscSelectedValid(validBits,source);
                    uint selectedValue=source==0u?value1:value2;
                    bool eligible=ready>=OSC_PRE_CAPACITY && previousValid!=0u && valid!=0u;
                    if(eligible && firstEligible==0xffffffffu)firstEligible=ordinal;
                    if(eligible && firstCrossing==0xffffffffu)
                    {
                        float before=asfloat(previousValue);
                        float current=asfloat(selectedValue);
                        bool crossed=_TriggerFalling!=0u ? before>_TriggerLevel && current<=_TriggerLevel
                            : before<_TriggerLevel && current>=_TriggerLevel;
                        if(crossed)firstCrossing=ordinal;
                    }
                    if(valid!=0u)
                    {
                        ready=min(OSC_PRE_CAPACITY,ready+1u);
                        previousValue=selectedValue;
                        previousValid=1u;
                    }
                    else
                    {
                        ready=0u;
                        previousValue=asuint(0.0);
                        previousValid=0u;
                    }
                }
                finalReady=ready;
                finalPreviousValue=previousValue;
                finalPreviousValid=previousValid;
            }

            void CountCaptureAppend(uint firstSequence,uint candidateCount,uint stride,uint latestSequence,uint source,
                uint startOrdinal,uint initialPost,out uint appendCount,out uint finalPost,out uint aborted)
            {
                appendCount=0u;finalPost=initialPost;aborted=0u;
                [loop]for(uint ordinal=0u;ordinal<OSC_RAW_CAPACITY;ordinal++)
                {
                    if(ordinal<startOrdinal)continue;
                    if(ordinal>=candidateCount || finalPost>=OSC_POST_CAPACITY)break;
                    uint timeValue,value1,value2,validBits;
                    OscLoadCandidate(firstSequence,ordinal,stride,latestSequence,timeValue,value1,value2,validBits);
                    if(OscSelectedValid(validBits,source)==0u)
                    {
                        aborted=1u;
                        finalPost=0u;
                        break;
                    }
                    appendCount++;
                    finalPost++;
                }
            }

            TriggerState BuildState()
            {
                TriggerState result;
                uint stride=max(1u,_Stride);
                uint rawCount=min(OSC_RAW_CAPACITY,OscSolverLoad(2u,0u));
                uint sequence=OscSolverLoad(3u,0u);
                uint generation=OscSolverLoad(6u,0u);
                uint oldSequence=StateLoad(OSC_STATE_SEQUENCE);
                uint oldGeneration=StateLoad(OSC_STATE_GENERATION);
                uint oldSampling=StateLoad(OSC_STATE_SAMPLING_REVISION);
                uint oldTrigger=StateLoad(OSC_STATE_TRIGGER_REVISION);
                uint oldAcquisition=StateLoad(OSC_STATE_ACQUISITION);
                uint oldPreCount=min(OSC_PRE_CAPACITY,StateLoad(OSC_STATE_PRE_COUNT));
                uint oldReady=min(OSC_PRE_CAPACITY,StateLoad(OSC_STATE_READY_COUNT));
                uint oldPreviousValue=StateLoad(OSC_STATE_PREVIOUS_VALUE);
                uint oldPreviousValid=StateLoad(OSC_STATE_PREVIOUS_VALID);
                uint oldPost=min(OSC_POST_CAPACITY,StateLoad(OSC_STATE_POST_COUNT));
                uint oldAuto=min(OSC_AUTO_FRAMES,StateLoad(OSC_STATE_AUTO_WAIT));
                uint oldEnabled=StateLoad(OSC_STATE_ENABLED)!=0u?1u:0u;
                uint oldHasTriggerDisplay=StateLoad(OSC_STATE_HAS_TRIGGER_DISPLAY)!=0u?1u:0u;

                bool generationChanged=generation!=oldGeneration;
                bool sequenceRewound=sequence<oldSequence;
                bool samplingChanged=_SamplingRevision!=oldSampling;
                bool overrun=!generationChanged && !sequenceRewound && sequence>oldSequence && sequence-oldSequence>rawCount;
                bool hardReset=_ForceReset!=0u || generationChanged || sequenceRewound || samplingChanged;
                bool resetPre=hardReset || overrun;
                bool triggerChanged=_TriggerRevision!=oldTrigger;
                uint oldest=rawCount==0u?sequence+1u:sequence-rawCount+1u;
                uint requestedStart=resetPre?oldest:oldSequence+1u;
                uint firstSequence=OscFirstAligned(requestedStart,stride);
                uint candidateCount=OscCandidateCount(requestedStart,sequence,stride);

                uint acquisition=oldAcquisition;
                uint preCount=resetPre?0u:oldPreCount;
                uint ready=resetPre?0u:oldReady;
                uint previousValue=resetPre?asuint(0.0):oldPreviousValue;
                uint previousValid=resetPre?0u:oldPreviousValid;
                uint postCount=resetPre?0u:oldPost;
                uint autoWait=resetPre?0u:oldAuto;
                uint hasTriggerDisplay=(hardReset || (oldEnabled==0u && _TriggerEnabled!=0u))?
                    0u:oldHasTriggerDisplay;
                uint flags=resetPre?OSC_FLAG_RESET_PRE:0u;
                if(hardReset)flags|=OSC_FLAG_CLEAR_DISPLAY;
                if(overrun)flags|=OSC_FLAG_OVERRUN;
                if(resetPre)flags|=OSC_FLAG_CLEAR_CAPTURE;
                if(triggerChanged)
                {
                    flags|=OSC_FLAG_TRIGGER_CHANGED|OSC_FLAG_CLEAR_CAPTURE;
                    postCount=0u;
                    autoWait=0u;
                    if(!resetPre)ReadReadyFromPre(preCount,min(_TriggerSource,1u),ready,previousValue,previousValid);
                    acquisition=_TriggerEnabled==0u?OSC_DISABLED:
                        (ready>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING);
                }

                result.sequence=sequence;
                result.generation=generation;
                result.samplingRevision=_SamplingRevision;
                result.triggerRevision=_TriggerRevision;
                result.candidateStart=firstSequence;
                result.candidateCount=candidateCount;
                result.triggerOrdinal=0u;
                result.appendStart=0u;
                result.appendCount=0u;
                result.oldPostCount=oldPost;
                result.oldPreCount=resetPre?0u:oldPreCount;
                result.mode=min(_TriggerMode,OSC_MODE_AUTO);
                result.enabled=_TriggerEnabled!=0u?1u:0u;

                uint finalReady,finalPreviousValue,finalPreviousValid,firstEligible,firstCrossing;
                ScanReadiness(firstSequence,candidateCount,stride,sequence,min(_TriggerSource,1u),
                    ready,previousValue,previousValid,finalReady,finalPreviousValue,finalPreviousValid,
                    firstEligible,firstCrossing);
                preCount=min(OSC_PRE_CAPACITY,preCount+candidateCount);

                if(resetPre)
                {
                    acquisition=_TriggerEnabled==0u?OSC_DISABLED:
                        (finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING);
                    postCount=0u;autoWait=0u;
                }
                else if(_TriggerEnabled==0u)
                {
                    acquisition=OSC_DISABLED;postCount=0u;autoWait=0u;
                }
                else if(acquisition==OSC_HOLD)
                {
                    autoWait=0u;
                }
                else if(acquisition==OSC_CAPTURING)
                {
                    uint appended,nextPost,aborted;
                    CountCaptureAppend(firstSequence,candidateCount,stride,sequence,min(_TriggerSource,1u),
                        0u,postCount,appended,nextPost,aborted);
                    result.appendStart=0u;
                    result.appendCount=appended;
                    if(aborted!=0u)
                    {
                        flags|=OSC_FLAG_CLEAR_CAPTURE;
                        postCount=0u;autoWait=0u;
                        acquisition=finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING;
                    }
                    else
                    {
                        postCount=nextPost;
                        if(postCount>=OSC_POST_CAPACITY)
                        {
                            flags|=OSC_FLAG_CAPTURE_COMPLETE;
                            hasTriggerDisplay=1u;
                            autoWait=0u;
                            acquisition=_TriggerMode==OSC_MODE_SINGLE?OSC_HOLD:
                                (finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING);
                        }
                    }
                }
                else
                {
                    uint nextAuto=0u;
                    if(_TriggerMode==OSC_MODE_AUTO && acquisition==OSC_WAITING && firstEligible!=0xffffffffu)
                        nextAuto=min(OSC_AUTO_FRAMES,autoWait+1u);
                    uint chosen=firstCrossing;
                    if(chosen==0xffffffffu && _TriggerMode==OSC_MODE_AUTO && nextAuto>=OSC_AUTO_FRAMES)
                        chosen=firstEligible;
                    if(chosen!=0xffffffffu)
                    {
                        uint appended,nextPost,aborted;
                        // A settings revision clears an older partial capture, but a
                        // new crossing from samples processed after that revision may
                        // immediately start a fresh one in the same frame.
                        flags&=~OSC_FLAG_CLEAR_CAPTURE;
                        CountCaptureAppend(firstSequence,candidateCount,stride,sequence,min(_TriggerSource,1u),
                            chosen,0u,appended,nextPost,aborted);
                        result.triggerOrdinal=chosen+1u;
                        result.appendStart=chosen;
                        result.appendCount=appended;
                        flags|=OSC_FLAG_TRIGGER_STARTED;
                        autoWait=0u;
                        if(aborted!=0u)
                        {
                            flags|=OSC_FLAG_CLEAR_CAPTURE;
                            postCount=0u;
                            acquisition=finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING;
                        }
                        else
                        {
                            postCount=nextPost;
                            if(postCount>=OSC_POST_CAPACITY)
                            {
                                flags|=OSC_FLAG_CAPTURE_COMPLETE;
                                hasTriggerDisplay=1u;
                                acquisition=_TriggerMode==OSC_MODE_SINGLE?OSC_HOLD:
                                    (finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING);
                            }
                            else acquisition=OSC_CAPTURING;
                        }
                    }
                    else
                    {
                        acquisition=finalReady>=OSC_PRE_CAPACITY?OSC_WAITING:OSC_PREPARING;
                        autoWait=acquisition==OSC_WAITING?nextAuto:0u;
                    }
                }

                result.acquisition=acquisition;
                result.preCount=preCount;
                result.readyCount=finalReady;
                result.previousValue=finalPreviousValue;
                result.previousValid=finalPreviousValid;
                result.postCount=postCount;
                result.autoWait=autoWait;
                result.flags=flags;
                result.hasTriggerDisplay=hasTriggerDisplay;
                return result;
            }

            uint frag(v2f input):SV_Target
            {
                uint x=(uint)input.vertex.x;
                TriggerState state=BuildState();
                if(x==OSC_STATE_SEQUENCE)return state.sequence;
                if(x==OSC_STATE_GENERATION)return state.generation;
                if(x==OSC_STATE_SAMPLING_REVISION)return state.samplingRevision;
                if(x==OSC_STATE_TRIGGER_REVISION)return state.triggerRevision;
                if(x==OSC_STATE_ACQUISITION)return state.acquisition;
                if(x==OSC_STATE_PRE_COUNT)return state.preCount;
                if(x==OSC_STATE_READY_COUNT)return state.readyCount;
                if(x==OSC_STATE_PREVIOUS_VALUE)return state.previousValue;
                if(x==OSC_STATE_PREVIOUS_VALID)return state.previousValid;
                if(x==OSC_STATE_POST_COUNT)return state.postCount;
                if(x==OSC_STATE_AUTO_WAIT)return state.autoWait;
                if(x==OSC_STATE_CANDIDATE_START)return state.candidateStart;
                if(x==OSC_STATE_CANDIDATE_COUNT)return state.candidateCount;
                if(x==OSC_STATE_TRIGGER_ORDINAL)return state.triggerOrdinal;
                if(x==OSC_STATE_APPEND_START)return state.appendStart;
                if(x==OSC_STATE_APPEND_COUNT)return state.appendCount;
                if(x==OSC_STATE_FLAGS)return state.flags;
                if(x==OSC_STATE_OLD_POST_COUNT)return state.oldPostCount;
                if(x==OSC_STATE_OLD_PRE_COUNT)return state.oldPreCount;
                if(x==OSC_STATE_MODE)return state.mode;
                if(x==OSC_STATE_ENABLED)return state.enabled;
                if(x==OSC_STATE_HAS_TRIGGER_DISPLAY)return state.hasTriggerDisplay;
                return 0u;
            }
            ENDCG
        }
    }
}
