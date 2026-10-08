#define FORMMODE_ROUNDED_BOX 0
#define FORMMODE_ASSET 1
#define EFFECT_NONE 0
#define EFFECT_SHADOW 1
#define EFFECT_OUTLINE 2
#define EFFECT_KIND_MASK 3u
#define EFFECT_SOFTNESS_SHIFT 2
#define EFFECT_SOFTNESS_MASK 3u

#include "../../TranslucentImage/Resources/Shaders/TranslucentImage.hlsl"
#include "../../TranslucentImage/Resources/Shaders/packing.hlsl"
#include "optics.hlsl"
#include "formSprite.hlsl"
#include "shape.hlsl"

uniform half _LeTai_CanvasScaleFactor;

uniform half3 _RefractiveIndexRatios;

uniform half4 _EdgeGlintDirections;
uniform half4 _EdgeGlint1Color;
uniform half4 _EdgeGlint2Color;
uniform half  _EdgeGlintWrap;
uniform half  _EdgeGlintSharpness;

struct ParaformConfig
{
    int   formMode;
    half2 center;
    half2 extent;
    half4 radii;
    half  cornerCurvature;
    half  filletCurvature;
    half  bevelWidth;
    half  halfRingThickness;
    half  elevation;
    half  distanceRange;
    int   effectKind;
    half  shadowSpread;
    half  shadowQuality;
    half  outlineInnerWidth;
    half  outlineOuterWidth;
    half  effectInnerSoftness;
    half  effectOuterSoftness;
    int   effectSoftnessType;
};

#if ENABLE_EFFECTS
#include "effects.hlsl"
#endif

void unpackVertexDataParaform(VertexInput i, inout VertexOutput o)
{
    float maxRadii         = min(i.packedData2[0], i.packedData2[1]);
    int   formMode         = (int)(asuint(i.packedData3[0]) >> 31);
    float distanceRange    = formMode == FORMMODE_ASSET ? -i.packedData3[0] : 0;
    float effectWidthRange = formMode == FORMMODE_ASSET ? max(distanceRange, 1e-6) : maxRadii;

    FloatUnpacker upk0 = CreateUnpacker(i.packedData3[0], 10);
    FloatUnpacker upk1 = CreateUnpacker(i.packedData3[1], 10);
    FloatUnpacker upk2 = CreateUnpacker(i.packedData3[2], 15);
    #if ENABLE_EFFECTS
    bool isEffect = upk1.sign;
    #endif

    o.transfer2 = i.packedData2;
    o.transfer3 = half4(
        DequeueNonNegative(upk0, maxRadii),
        DequeueNonNegative(upk0, maxRadii),
        DequeueNonNegative(upk0, maxRadii),
        DequeueNonNegative(upk1, maxRadii)
    );
    o.transfer4.x = Dequeue(upk1, 1.001, 7, 8);
    o.transfer5   = half2(
        DequeueNonNegative(upk2, effectWidthRange),
        formMode == FORMMODE_ASSET ? -distanceRange : 0
    );

    #if ENABLE_EFFECTS
    UNITY_BRANCH
    if (isEffect)
    {
        float         valueRange  = max(i.packedData2[0], i.packedData2[1]);
        FloatUnpacker effectData  = CreateUnpacker(i.packedData3[3]);
        uint          effectFlags = (uint)round(DequeueNonNegative(effectData, 15, 4));

        UNITY_BRANCH
        if ((effectFlags & EFFECT_KIND_MASK) == EFFECT_SHADOW)
        {
            float spread   = Dequeue(upk1, -valueRange, valueRange, 12);
            float softness = DequeueNonNegative(effectData, valueRange, 12);
            float quality  = DequeueNonNegative(effectData, 1, 1);
            o.transfer1    = float4(spread, quality, softness, softness);
        }
        else
        {
            o.transfer1 = float4(
                Dequeue(upk1, -valueRange, valueRange, 12),
                Dequeue(effectData, -valueRange, valueRange, 12),
                DequeueNonNegative(effectData, valueRange, 12),
                DequeueNonNegative(upk2, valueRange, 12)
            );
        }

        o.transfer4.y  = -(float)effectFlags;
        o.transfer4.zw = half2(1, 0);
        o.texcoord.z   = 0;
    }
    else
    #endif
    {
        FloatUnpacker upk3 = CreateUnpacker(i.packedData3[3], 15);
        o.transfer4.y      = Dequeue(upk1, 1.001, 7, 8);
        o.transfer4.z      = DequeueNonNegative(upk3, effectWidthRange);
        o.transfer4.w      = DequeueNonNegative(upk3, 1000);
    }
}

ParaformConfig createParaformConfig(VertexOutput vertex)
{
    ParaformConfig data;

    data.formMode = (int)(asuint((float)vertex.transfer5.y) >> 31);
    data.extent   = vertex.transfer2.xy;
    data.center   = vertex.transfer2.zw;

    data.radii = vertex.transfer3;

    data.cornerCurvature = vertex.transfer4.x;
    data.filletCurvature = vertex.transfer4.y;
    data.bevelWidth      = vertex.transfer4.z;
    data.elevation       = vertex.transfer4.w;

    data.halfRingThickness = vertex.transfer5.x;
    data.distanceRange     = abs(vertex.transfer5.y);

    #if ENABLE_EFFECTS
    bool isEffect            = vertex.transfer4.y < 0;
    uint effectFlags         = isEffect ? (uint)round(-vertex.transfer4.y) : 0u;
    data.effectKind          = (int)(effectFlags & EFFECT_KIND_MASK);
    data.effectSoftnessType  = (int)((effectFlags >> EFFECT_SOFTNESS_SHIFT) & EFFECT_SOFTNESS_MASK);
    data.shadowSpread        = isEffect ? vertex.transfer1.x : 0;
    data.shadowQuality       = isEffect ? vertex.transfer1.y : 0;
    data.outlineInnerWidth   = isEffect ? vertex.transfer1.x : 0;
    data.outlineOuterWidth   = isEffect ? vertex.transfer1.y : 0;
    data.effectInnerSoftness = isEffect ? vertex.transfer1.z : 0;
    data.effectOuterSoftness = isEffect ? vertex.transfer1.w : 0;
    #endif

    return data;
}

void paraformFragSetup(
    ParaformConfig     config,
    inout VertexOutput IN,
    out half2          screenPos,
    out half4          foregroundColor,
    out Appearance     appearance
)
{
    #if ENABLE_EFFECTS
    if (config.effectKind != EFFECT_NONE)
        fragSetup(IN, 1, screenPos, foregroundColor, appearance);
    else if (config.formMode == FORMMODE_ASSET)
    #else
    if (config.formMode == FORMMODE_ASSET)
    #endif
        fragSetup(IN, 1, screenPos, foregroundColor, appearance);
    else
        fragSetup(IN, screenPos, foregroundColor, appearance);
}

struct ShapeBoundary
{
    half  edgeDistance;
    half  distToPx;
    half2 screenGradient;
    half  screenGradientLenRcp;
};

ShapeBoundary getBoundary(ParaformConfig config, float2 uv)
{
    ShapeBoundary boundary;
    half2         gradient;
    UNITY_BRANCH
    if (config.formMode == FORMMODE_ASSET)
    {
        float4 tex            = tex2D(_MainTex, uv);
        boundary.edgeDistance = decodeDist(tex.ba, config.distanceRange) + config.halfRingThickness;
        gradient              = decodeGrad(tex.rg);
        gradient              = normalize(gradient);
    }
    else
    {
        float encodedRadius = selectCorner(config.center, config.radii);
        float cornerPower   = config.cornerCurvature;
        float realRadius    = encodedRadius;
        #if ENABLE_EFFECTS
        realRadius = effectShapeRadius(config, encodedRadius, cornerPower);
        #endif

        roundedBox(config.center,
                   config.extent - config.halfRingThickness,
                   realRadius - config.halfRingThickness,
                   cornerPower,
                   boundary.edgeDistance,
                   gradient);
    }

    UNITY_BRANCH
    if (config.halfRingThickness > 0)
        ringify(config.halfRingThickness, boundary.edgeDistance, gradient);

    float2 dpdy = ddy(config.center);
    #if UNITY_UV_STARTS_AT_TOP
    dpdy *= -_ProjectionParams.x;
    #endif
    boundary.screenGradient.y = dot(gradient, dpdy);
    boundary.screenGradient.x = dot(gradient, ddx(config.center));

    boundary.screenGradientLenRcp = rsqrtApprox01(dot(boundary.screenGradient, boundary.screenGradient));
    half transitionWidth          = sqrt(2);
    boundary.distToPx             = (1. / transitionWidth) * boundary.screenGradientLenRcp;
    return boundary;
}

struct ShapeSurface
{
    half  alpha;
    half  zEdge;
    half3 normal;
    half  normalXYLen;
};

ShapeSurface getSurface(ParaformConfig config, ShapeBoundary boundary)
{
    ShapeSurface surface;
    half         edgeFactor = 1. - saturate(-boundary.edgeDistance / config.bevelWidth);

    half2 normalEdge;
    edgeParams(edgeFactor, config.filletCurvature, surface.zEdge, normalEdge);
    surface.normal.z    = -normalEdge.y;
    surface.normal.xy   = boundary.screenGradient * boundary.screenGradientLenRcp * normalEdge.x;
    surface.normalXYLen = normalEdge.x;
    surface.alpha       = saturate(-boundary.edgeDistance * boundary.distToPx);
    return surface;
}

half3 sampleRefractedBackground(float2 screenPos, half3 normal, half distance)
{
    half3 backgroundColor;
    #if _REFRACTION_MODE_OFF
    backgroundColor = sampleBackground(screenPos);
    #else
    #if _REFRACTION_MODE_ON
    half2 offset = GetRefractedScreenOffset(normal, distance, _RefractiveIndexRatios[0]);
    backgroundColor = sampleBackground(screenPos + offset);
    #elif _REFRACTION_MODE_CHROMATIC
    UNITY_UNROLL for (int i = 0; i < 3; ++i)
    {
        half2 offset       = GetRefractedScreenOffset(normal, distance, _RefractiveIndexRatios[i]);
        backgroundColor[i] = sampleBackground(screenPos + offset)[i];
    }
    #endif
    #endif

    return backgroundColor;
}

half3 specular(half2 normalXY, half normalXYLen, half2 halfDir)
{
    half gterm = max(0, dot(normalXY, halfDir));
    half spec  = pow(gterm, lerp(_EdgeGlintSharpness, _EdgeGlintWrap, normalXYLen));

    return spec;
}

half3 blendEdge(half3 color, half2 normalXY, half normalXYLen)
{
    #if _USE_EDGE_GLINT

    // halfdir.z = 0 work well
    half3 edgeGlint1 = specular(normalXY, normalXYLen, _EdgeGlintDirections.xy) * _EdgeGlint1Color.rgb;
    half3 edgeGlint2 = specular(normalXY, normalXYLen, _EdgeGlintDirections.zw) * _EdgeGlint2Color.rgb;

    color = saturate(color + edgeGlint1 + edgeGlint2);

    #endif

    return color;
}

half4 paraformMain(ParaformConfig config, float2 uv, float2 screenPos, half4 foregroundColor, Appearance appearance)
{
    half4 color = foregroundColor;
    ShapeBoundary boundary = getBoundary(config, uv);

    #if ENABLE_EFFECTS
    UNITY_BRANCH
    if (config.effectKind != EFFECT_NONE)
    {
        color.a *= effectAlpha(config, boundary.edgeDistance, boundary.distToPx);
    }
    else
    #endif
    {
        ShapeSurface surface = getSurface(config, boundary);

        #ifndef _BACKGROUND_MODE_OPAQUE
        half backgroundZDistance = surface.zEdge * config.bevelWidth + config.elevation;
        backgroundZDistance *= _LeTai_CanvasScaleFactor;
        half3 backgroundColor = sampleRefractedBackground(screenPos, surface.normal, backgroundZDistance);
        // half fresnelTerm = pow(1 - saturate(-normal.z), 5);
        // backgroundColor *= 1 - fresnelTerm;
        color = blendBackground(foregroundColor, backgroundColor, appearance);
        #endif

        color.rgb = blendEdge(color.rgb, surface.normal.xy, surface.normalXYLen);
        color.a *= surface.alpha;
    }

    // color.rgb = backgroundColor;
    // color.rgb = 0;
    // color = float4(surface.alpha.xxx, 1);
    // color.rgb = (surface.normal + 1) / 2;
    // color.rgb = surface.zEdge;
    // color.rgb = fresnelTerm;
    // color.rgb = blendEdge(0, surface.normal.xy, surface.normalXYLen);
    // color.rgb = surface.normalXYLen;
    // half2 offset = GetRefractedScreenOffset(surface.normal, backgroundZDistance, _RefractiveIndexRatios[0]);
    // color.rgb = sampleBackground(screenPos + offset);
    // color.rg = abs(offset);
    // color.rg = screenPos + offset;
    // color.b = 0;


    return color;
}
