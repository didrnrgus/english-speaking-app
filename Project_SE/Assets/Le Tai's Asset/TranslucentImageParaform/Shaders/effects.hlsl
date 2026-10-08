#define GAUSSIAN_SIGMA_RANGE 3
#define SHADOW_QUALITY_HIGH 1
#define SOFTNESS_SIMPLE 0
#define SOFTNESS_GAUSSIAN 1
#define SOFTNESS_LIGHT 2

void shadowMorph(inout float apparentRadius, inout float cornerPower, float sigma)
{
    float decay    = exp(-apparentRadius / (4.12 * sigma));
    float decay2   = decay * decay;
    cornerPower    = 2 + (cornerPower - 2) * (1 - decay) + decay2 * decay2 * decay;
    apparentRadius = sqrt(apparentRadius * apparentRadius + 1.96 * 1.96 * sigma * sigma);
}

float effectShapeRadius(ParaformConfig config, float encodedRadius, inout float cornerPower)
{
    float shapeRadius = encodedRadius;

    UNITY_BRANCH
    if (config.effectKind == EFFECT_SHADOW && config.shadowQuality == SHADOW_QUALITY_HIGH)
    {
        float apparentRadius = encodedRadius;
        shadowMorph(apparentRadius, cornerPower, config.effectOuterSoftness / GAUSSIAN_SIGMA_RANGE);
        shapeRadius = min(apparentToRealRadius(apparentRadius, cornerPower), min(config.extent.x, config.extent.y));
    }

    return shapeRadius;
}

half attenuation(half x)
{
    const float a2 = pow(.1, 2);

    float x2     = x * x;
    float yuksel = 1 - x * rsqrt(x2 + a2);
    float window = saturate(1.0 - x2 * x2);
    window *= window;

    return yuksel * window;
}

half falloffProfile(half x, int softnessType)
{
    half alpha;

    UNITY_BRANCH
    if (softnessType == SOFTNESS_SIMPLE)
        alpha = 1 - smoothstep(0, 1, x);
    else if (softnessType == SOFTNESS_GAUSSIAN)
    {
        half sigmaDistance = x * GAUSSIAN_SIGMA_RANGE;
        half gaussian      = exp(-.5 * sigmaDistance * sigmaDistance);

        const half tail = .011109;

        alpha = saturate((gaussian - tail) * (1 / (1 - tail)));
    }
    else
        alpha = attenuation(x);

    return alpha;
}

half inverseLerp(half from, half to, half value)
{
    return (value - from) / (to - from);
}

half effectAlpha(ParaformConfig config, half edgeDistance, half distToPx)
{
    half alpha = 0;

    UNITY_BRANCH
    if (config.effectKind == EFFECT_SHADOW)
    {
        half shadowDistancePx   = (edgeDistance - config.shadowSpread) * distToPx;
        half shadowSoftnessPx   = config.effectOuterSoftness * distToPx;
        half shadowFadeProgress = saturate(inverseLerp(-1, shadowSoftnessPx, shadowDistancePx));
        alpha = falloffProfile(shadowFadeProgress, config.effectSoftnessType);
    }
    else
    {
        half minExtentPx        = min(config.extent.x, config.extent.y) * distToPx;
        half outerOverlapPixels = min(.75, minExtentPx * 2 * .05);
        half innerOverlapPixels = min(.45, minExtentPx * 2 * .05);

        half innerWidthPx     = config.outlineInnerWidth * distToPx;
        half outerWidthPx     = config.outlineOuterWidth * distToPx;
        half innerSoftnessPx  = config.effectInnerSoftness * distToPx;
        half outerSoftnessPx  = config.effectOuterSoftness * distToPx;
        half edgeDistancePx   = edgeDistance * distToPx;
        half outlineSpanPx    = innerWidthPx + outerWidthPx + innerSoftnessPx + outerSoftnessPx;
        half overlapFactor    = saturate(outlineSpanPx);
        half innerEdgePx      = -innerWidthPx - innerOverlapPixels * overlapFactor;
        half outerEdgePx      = outerWidthPx + outerOverlapPixels * overlapFactor;
        half sampleDistancePx = edgeDistancePx + .5;
        half innerSoftBlend   = saturate(innerSoftnessPx);
        half outerSoftBlend   = saturate(outerSoftnessPx);

        half innerFadeProgress = saturate(inverseLerp(0, max(innerSoftnessPx, 1), innerEdgePx - sampleDistancePx));
        half outerFadeProgress = saturate(inverseLerp(0, max(outerSoftnessPx, 1), sampleDistancePx - outerEdgePx));
        half innerSoftAlpha    = 1;
        half outerSoftAlpha    = 1;
        // UNITY_BRANCH
        // if (config.outlineInnerWidth + config.outlineOuterWidth >= 0)
        // {
        half softAlpha = falloffProfile(max(innerFadeProgress, outerFadeProgress), config.effectSoftnessType);
        innerSoftAlpha = innerFadeProgress > 0 ? softAlpha : 1;
        outerSoftAlpha = outerFadeProgress > 0 ? softAlpha : 1;
        // }
        // else
        // {
        //     innerSoftAlpha = falloffProfile(innerFadeProgress, config.effectSoftnessType);
        //     outerSoftAlpha = falloffProfile(outerFadeProgress, config.effectSoftnessType);
        // }
        // 2nd branch is needed for correctness but also who tf care

        half innerAlpha       = lerp(saturate(edgeDistancePx + 1 - innerEdgePx), innerSoftAlpha, innerSoftBlend);
        half outerAlpha       = lerp(saturate(outerEdgePx - edgeDistancePx), outerSoftAlpha, outerSoftBlend);
        half thinOutlineAlpha = saturate((1 + innerOverlapPixels + outerOverlapPixels) * outlineSpanPx);
        alpha = min(thinOutlineAlpha, min(innerAlpha, outerAlpha));
    }

    return alpha;
}
