// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

#ifndef FORM_SPRITE_ALPHA_MORPH_INCLUDED
#define FORM_SPRITE_ALPHA_MORPH_INCLUDED

#include "formSprite.hlsl"

float sampleFormSpriteDistance(sampler2D textureSampler, float2 uv, float distanceRange)
{
    return decodeDist(tex2D(textureSampler, uv).ba, distanceRange);
}

float formSpriteAlpha(float distance, float inversePixelWidth)
{
    return saturate(.5 - distance * inversePixelWidth);
}

float sampleFormSpriteDistance(
    sampler2D textureSampler,
    float2    uv,
    float4    spriteUVRect,
    float     distanceRange
)
{
    return sampleFormSpriteDistance(textureSampler,
                                        spriteUVRect.xy + uv * spriteUVRect.zw,
                                        distanceRange);
}

float formSpriteMorphAlpha(
    sampler2D textureSampler,
    float2    uv,
    float2    sourceSize,
    float4    spriteUVRect,
    float2    spriteSize,
    float     distanceRange,
    float     centerDistance,
    float     widthMorph,
    float     positionMorph
)
{
    float2 inverseSourceSize = rcp(sourceSize);
    float2 gridPosition      = uv * sourceSize - .5;
    float2 cell              = floor(gridPosition);
    float2 blend             = frac(gridPosition);

    float2 uv00 = (cell + .5) * inverseSourceSize;
    float2 uv10 = uv00 + float2(inverseSourceSize.x, 0);
    float2 uv01 = uv00 + float2(0, inverseSourceSize.y);
    float2 uv11 = uv00 + inverseSourceSize;

    float positionError = 1 - positionMorph;
    uv00                = lerp(uv, uv00, positionError);
    uv10                = lerp(uv, uv10, positionError);
    uv01                = lerp(uv, uv01, positionError);
    uv11                = lerp(uv, uv11, positionError);

    float coarseWidth       = min(spriteSize.x * inverseSourceSize.x,
                                  spriteSize.y * inverseSourceSize.y);
    float nativeWidth       = fwidth(centerDistance);
    float inversePixelWidth = rcp(max(lerp(coarseWidth, nativeWidth, widthMorph), 1e-5));

    float alpha00 = formSpriteAlpha(
        sampleFormSpriteDistance(textureSampler, uv00, spriteUVRect, distanceRange),
        inversePixelWidth);
    float alpha10 = formSpriteAlpha(
        sampleFormSpriteDistance(textureSampler, uv10, spriteUVRect, distanceRange),
        inversePixelWidth);
    float alpha01 = formSpriteAlpha(
        sampleFormSpriteDistance(textureSampler, uv01, spriteUVRect, distanceRange),
        inversePixelWidth);
    float alpha11 = formSpriteAlpha(
        sampleFormSpriteDistance(textureSampler, uv11, spriteUVRect, distanceRange),
        inversePixelWidth);

    return lerp(lerp(alpha00, alpha10, blend.x),
                lerp(alpha01, alpha11, blend.x),
                blend.y);
}

#endif
