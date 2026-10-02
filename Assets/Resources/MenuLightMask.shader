Shader "Hidden/CandyCruisers/MenuLightMask"
{
    Properties { _MainTex ("Artwork", 2D) = "white" {} }
    SubShader { Pass {
        ZTest Always ZWrite Off Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        Output vert(Input v) { Output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
        fixed4 frag(Output i) : SV_Target { return fixed4(1, 1, 1, tex2D(_MainTex, i.uv).a * i.color.a); }
        ENDCG
    } }
}
