Shader "VRCSpice/Oscilloscope Waveform"
{
    Properties
    {
        _DisplayDump ("Oscilloscope display dump", 2D) = "black" {}
        _Channel ("Channel", Integer) = 0
        _YScale ("Voltage scale", Float) = 0.125
        [HDR] _LineColor ("Line color", Color) = (0,1,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #include "UnityCG.cginc"

            Texture2D<uint> _DisplayDump;
            int _Channel;
            float _YScale;
            float4 _LineColor;
            struct appdata { float4 vertex:POSITION; };
            struct v2g { float4 vertex:POSITION; };
            struct g2f { float4 vertex:SV_POSITION; float localY:TEXCOORD0; };
            v2g vert(appdata v){v2g o;o.vertex=v.vertex;return o;}

            // Two point primitives split the display at its centre. Keeping
            // each invocation at 101 vertices avoids the lower geometry output
            // limit used by the VR rendering path while overlapping two points
            // so the trace remains continuous.
            [maxvertexcount(101)]
            void geom(point v2g input[1],inout LineStream<g2f> stream)
            {
                uint count=min(200u,_DisplayDump.Load(int3(0,0,0)));
                if(count==0u)return;
                uint channel=(uint)clamp(_Channel,0,1);
                uint bit=1u<<channel;
                uint segment=(uint)clamp(round(input[0].vertex.x),0.0,1.0);
                uint firstDisplaySlot=200u-count;
                uint segmentFirst=segment==0u?0u:99u;
                uint segmentEnd=segment==0u?101u:200u;
                uint firstSlot=max(firstDisplaySlot,segmentFirst);
                [loop]for(uint slot=firstSlot;slot<segmentEnd;slot++)
                {
                    uint draw=slot-firstDisplaySlot;
                    uint history=count-1u-draw;
                    uint valid=_DisplayDump.Load(int3(3,history+1u,0));
                    if((valid&bit)==0u)
                    {
                        stream.RestartStrip();continue;
                    }
                    float value=asfloat(_DisplayDump.Load(int3(channel+1u,history+1u,0)));
                    float x=-0.5+(float)slot/199.0;
                    float y=value*_YScale;
                    g2f o;o.vertex=UnityObjectToClipPos(float4(x,y,0,1));o.localY=y;
                    stream.Append(o);
                }
                stream.RestartStrip();
            }
            float4 frag(g2f input):SV_Target
            {
                clip(0.5-abs(input.localY));
                return _LineColor;
            }
            ENDCG
        }
    }
}
