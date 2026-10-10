Shader "Hidden/VRCSpice/Oscilloscope Trigger Test Dump"
{
    Properties
    {
        _Sequence ("Sequence", Integer) = 0
        _Count ("Count", Integer) = 0
        _Generation ("Generation", Integer) = 1
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend Off
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            uint _Sequence,_Count,_Generation;
            struct appdata{float4 vertex:POSITION;};
            struct v2f{float4 vertex:SV_POSITION;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);return o;}
            uint frag(v2f input):SV_Target
            {
                uint2 pixel=(uint2)input.vertex.xy;
                if(pixel.y==0u)
                {
                    if(pixel.x==0u)return 1u;
                    if(pixel.x==1u)return 2u;
                    if(pixel.x==2u)return min(1000u,_Count);
                    if(pixel.x==3u)return _Sequence;
                    if(pixel.x==4u)return asuint(0.001);
                    if(pixel.x==5u)return asuint((float)_Sequence*0.001);
                    if(pixel.x==6u)return _Generation;
                    return 0u;
                }
                uint history=pixel.y-1u;
                if(history>=_Count)return 0u;
                uint sampleSequence=_Sequence-history;
                float value=(float)((int)sampleSequence-101);
                if(pixel.x==0u)return asuint(value);
                if(pixel.x==1u)return asuint(-value);
                if(pixel.x==2u)return asuint((float)sampleSequence*0.001);
                return 0u;
            }
            ENDCG
        }
    }
}
