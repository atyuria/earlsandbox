// Vertex-coloured lines drawn with GL (x-ray wound tracks): always on top, alpha blended.
Shader "Hidden/Carl/Lines"
{
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct A { float4 vertex : POSITION; float4 color : COLOR; };
            struct V { float4 pos : SV_POSITION; float4 color : COLOR; };
            V vert(A v) { V o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            float4 frag(V i) : SV_Target { return i.color; }
            ENDHLSL
        }
    }
}
