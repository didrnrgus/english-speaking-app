#ifndef FORM_SPRITE_GENERATE_FILTER_INCLUDED
#define FORM_SPRITE_GENERATE_FILTER_INCLUDED

#include "../Shaders/formSprite.hlsl"
#include "ShaderDebugger.hlsl"

float _SmoothingRadius;

half4 filterFrag(v2f i) : SV_Target
{
    DBG_INIT(i.pos.xy, _LeTai_DbgPixelTarget.xy);

    int2   centerCoord = int2(i.pos.xy);
    float4 center      = _MainTex.Load(int3(centerCoord, 0));
    float  radius      = _SmoothingRadius;
    if (radius == 0)
        return center;

    float sigma_spatial = radius / 3.0;

    float2 gradCenter = decodeGrad(center.rg);
    float  distCenter = decodeDist(center.ba);
    float2 sumGrad    = gradCenter;
    float  sumWeight  = 1.0;

    float inv_2sigma_spatial = 1.0 / (2.0 * sigma_spatial * sigma_spatial);

    for (int y = -radius; y <= radius; y++)
    {
        for (int x = -radius; x <= radius; x++)
        {
            if (x == 0 && y == 0) continue;

            int2 offset      = int2(x, y);
            int2 sampleCoord = centerCoord + offset;
            bool inBounds    = all(sampleCoord >= int2(0, 0)) && all(sampleCoord < int2(_MainTex_TexelSize.zw));
            if (!inBounds) continue;

            float4 sample     = _MainTex.Load(int3(sampleCoord, 0));
            float2 gradSample = decodeGrad(sample.rg);

            float spatial_dist2 = dot(offset, offset);

            float w = exp(
                -spatial_dist2 * inv_2sigma_spatial
            );

            float2 dir = normalize(float2(offset));
            float  m   = 0.5 * dot(gradSample - gradCenter, dir);

            float wGeometry = saturate(1 + min(m, 0.0));

            w *= wGeometry;

            DBG_CIRCLE(sampleCoord, lerp(.0, .5, w), DBG_COL_CYAN);

            sumGrad += gradSample * w;
            sumWeight += w;
        }
    }

    float2 filtered = sumGrad / sumWeight;
    // filtered        = all(filtered == 0) ? gradCenter : /*normalize*/(filtered);
    // filtered = normalize(filtered);

    return half4(encodeGrad(filtered), center.ba);
}

#endif
