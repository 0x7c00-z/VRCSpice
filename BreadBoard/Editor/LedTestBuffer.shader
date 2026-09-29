Shader "Hidden/Breadboard/LED Test Buffer"
{
 SubShader { Pass { ZTest Always ZWrite Off Cull Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 4.5
 #include "UnityCG.cginc"
 uint frag(v2f_img i):SV_Target {
 uint2 p=(uint2)(i.uv*32);
 if(p.y!=13)return 0;
 return asuint(p.x==2?.005f:p.x==5?.02f:p.x==6?-.02f:0.f);
 }
 ENDCG
 } }
}
