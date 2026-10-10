Shader "Unlit/FlowControl"
{
    Properties
    {
        _DATA_N ("MNA vector size", Integer) = 1
        _SYSTEM_N ("Newton system size", Integer) = 3
        _STAGE_COUNT ("Stage count", Integer) = 3
        _IntegrationScheme ("0 Radau IIA5, 1 Backward Euler", Integer) = 0
        _MaxNewtonIterations ("Newton iteration limit", Integer) = 32
        _OutputDeltaTime ("Uniform output interval", Float) = 0.01
        _SettingsRevision ("Settings revision", Integer) = 0
        _ClearHistoryRevision ("History revision", Integer) = 0
        _RestartRevision ("Restart revision", Integer) = 0
        [HideInInspector] _BufferTemplate ("Solver Buffer Template", 2D) = "black" {}
        _A ("Static matrix", 2D) = "black" {}
        _B ("Charge matrix", 2D) = "black" {}
        _C ("Nonlinear metadata", 2D) = "black" {}
        _Is ("Source metadata", 2D) = "black" {}
        _rhs ("Right hand side", 2D) = "black" {}
        _TimeEvolution ("D, P/h and c", 2D) = "black" {}
        _MainTex ("Solver state", 2D) = "black" {}
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
            #include "Solver.hlsl"
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            uint frag(v2f i):SV_Target{return flowControl(uv2texel(i.uv));}
            ENDCG
        }
    }
}
