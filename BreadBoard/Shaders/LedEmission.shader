Shader "Breadboard/LED Current Emission"
{
    Properties
    {
        [HideInInspector] _MainTex ("Solver buffer", 2D) = "black" {}
        [HideInInspector] _DATA_N ("Solver size", Integer) = 0
        [HideInInspector] _CurrentRow ("Current row", Integer) = -1
        _BaseColor ("Base color", Color) = (0.12,0.025,0.02,1)
        [HDR] _EmissionColor ("Emission color", Color) = (1,0.025,0.005,1)
        _CurrentGain ("Emission per ampere", Float) = 100
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Blend Off
            ZWrite On
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "../../MNATools/Shader/Solver.hlsl"
            int _CurrentRow;
            float4 _BaseColor, _EmissionColor;
            float _CurrentGain;
            struct appdata { float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex);return o;
            }
            float4 frag(v2f i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if(_CurrentRow<0 || (uint)_CurrentRow>=_DATA_N)return float4(_BaseColor.rgb,1);
                // get_data(history offset, vector row): latest accepted current.
                float current=get_data(0,(uint)_CurrentRow);
                if(!isfinite(current) || current<=0)return float4(_BaseColor.rgb,1);
                return float4(_BaseColor.rgb+_EmissionColor.rgb*current*_CurrentGain,1);
            }
            ENDCG
        }
    }
}
