// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

Shader "Hidden/Paraform/Form-Sprite-Preview"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _WidthMorph ("Width Morph", Range(0, 1)) = 1
        _PositionMorph ("Position Morph", Range(0, 1)) = 1
        _GradientMorph ("Gradient Morph", Range(0, 1)) = 1
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 10
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
        }
        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../Shaders/formSpriteAlphaMorph.hlsl"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float2    _SourceSize;
            float     _WidthMorph;
            float     _PositionMorph;
            float     _GradientMorph;

            static const float  GRADIENT_BAND              = .08;
            static const float  EASE_K                     = 4;
            static const float  RANGE_STRIPE_PERIOD_PIXELS = 20;
            static const float3 RANGE_STRIPE_COLOR         = float3(.8, .1, .1);

            float easeIn(float progress)
            {
                return (exp(EASE_K * saturate(progress)) - 1) / (exp(EASE_K) - 1);
            }

            float inverseLerp(float from, float to, float value)
            {
                return (value - from) / (to - from);
            }

            float3 previewGradient(float2 gradient, float distance, float2 uv)
            {
                float  depth      = max(-distance, 0);
                float2 pixelWidth = fwidth(uv) * _MainTex_TexelSize.zw * 4;
                float  rangeEdge  = max(max(pixelWidth.x, pixelWidth.y), 1e-5);
                gradient *= 1 - smoothstep(_DistanceRange - rangeEdge, _DistanceRange, depth);

                float band   = GRADIENT_BAND * min(_MainTex_TexelSize.z, _MainTex_TexelSize.w) * .5;
                float front  = lerp(-2 * band, _DistanceRange + 2 * band, easeIn(_GradientMorph));
                float formed = 1 - smoothstep(front - band, front + band, depth);
                return float3(gradient * .5 + .5, 1) * formed;
            }

            float outsideRangeStripeCoverage(float distance, float2 screenPosition)
            {
                float diagonal = dot(screenPosition, normalize(float2(1, 1)));
                float phase    = frac(diagonal / RANGE_STRIPE_PERIOD_PIXELS);
                float stripe   = 1 - step(.5, phase);
                return step(_DistanceRange, distance) * stripe;
            }

            half4 frag(v2f_img input) : SV_Target
            {
                float4 encoded  = tex2D(_MainTex, input.uv);
                float  distance = decodeDist(encoded.ba);

                float alpha = formSpriteMorphAlpha(
                    _MainTex,
                    input.uv,
                    _SourceSize,
                    float4(0, 0, 1, 1),
                    _MainTex_TexelSize.zw,
                    _DistanceRange,
                    distance,
                    easeIn(_WidthMorph),
                    easeIn(_PositionMorph)
                );

                half4 preview   = half4(previewGradient(decodeGrad(encoded.rg), distance, input.uv), alpha);
                float stripeViz = easeIn(saturate(inverseLerp(.8, 1, _GradientMorph)));
                float stripe    = outsideRangeStripeCoverage(distance, input.pos.xy) * stripeViz;
                return lerp(preview, half4(RANGE_STRIPE_COLOR, 1), stripe);
            }
            ENDCG
        }
    }
}
