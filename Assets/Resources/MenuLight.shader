Shader "Hidden/CandyCruisers/MenuLight"
{
    Properties { _MainTex ("Silhouettes", 2D) = "black" {} }
    SubShader { Pass {
        ZTest Always ZWrite Off Cull Off Blend Off
        CGPROGRAM
        #pragma vertex vert_img
        #pragma fragment frag
        #pragma target 3.0
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _Source;
        float4 _BlockRange;
        float4 _LowerEdge;
        float _Reveal;
        fixed4 frag(v2f_img i) : SV_Target
        {
            float2 delta = i.uv - _Source.xy;
            float2 metric = delta * float2(_Source.z, 1);
            float distance = length(metric);
            // Integrate one luminous subtitle strip. Artwork, not authored rays, shapes the light.
            float visibility = 0;
            for (int emitter = 0; emitter < 7; emitter++)
            {
                float2 source = _Source.xy;
                source.x += (emitter / 6.0 - .5) * _Source.w / _Source.z * .85;
                float blocked = 0;
                // Spend samples only where artwork exists, avoiding stair steps on long projections.
                float start = saturate((_BlockRange.x - source.y) / max(.00001, i.uv.y - source.y));
                float end = saturate((_BlockRange.y - source.y) / max(.00001, i.uv.y - source.y));
                for (int s = 1; s <= 128; s++)
                {
                    float2 samplePoint = lerp(source, i.uv, lerp(start, end, (s - .5) / 128.0));
                    // Backlit artwork emits through each opening independently. A lower stroke
                    // must not seal the counter above it as an in-plane ray test would.
                    blocked += tex2D(_MainTex, samplePoint).r / 128.0;
                }
                visibility += 1 - blocked;
            }
            visibility /= 7;
            // Keep tiny antialiased edge leaks from brightening otherwise solid shadows.
            visibility = saturate((visibility - .025) / .975);
            float upper = smoothstep(-.025, .02, metric.y);
            float field = exp(-distance * 1.8) * upper;
            float glow = exp(-abs(metric.y) * 95) * exp(-pow(metric.x / max(.05, _Source.w * .42), 4));
            float illumination = field * pow(visibility, 4) * 1.5 + glow * .25;
            float outside = max(0, abs(metric.x) - _Source.w * .5);
            float edgeHeight = _LowerEdge.x + outside * _LowerEdge.y;
            illumination *= smoothstep(-.002, .002, i.uv.y - edgeHeight);
            // The rising subtitle tilts its light from a flat strip toward the top of the screen.
            float sweep = lerp(-.02, 1.15, _Reveal);
            illumination *= 1 - smoothstep(sweep - .045, sweep + .045, metric.y);
            return fixed4(1, 1, 1, saturate(illumination));
        }
        ENDCG
    } }
}
