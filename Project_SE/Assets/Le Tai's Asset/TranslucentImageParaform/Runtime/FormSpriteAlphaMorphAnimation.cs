// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.Paraform
{
public static class FormSpriteAlphaMorphAnimation
{
    const float POSITION_DELAY_FACTOR = .33f;

    static readonly int SOURCE_SIZE    = Shader.PropertyToID("_SourceSize");
    static readonly int WIDTH_MORPH    = Shader.PropertyToID("_WidthMorph");
    static readonly int POSITION_MORPH = Shader.PropertyToID("_PositionMorph");

    public static void Apply(Material material,
                             Vector2 sourceSize,
                             float elapsed,
                             float initialDelay,
                             float duration)
    {
        var positionDelay = initialDelay + duration * POSITION_DELAY_FACTOR;
        material.SetVector(SOURCE_SIZE, new Vector4(sourceSize.x, sourceSize.y, 0, 0));
        material.SetFloat(WIDTH_MORPH,    StageMorph(elapsed, initialDelay,  duration));
        material.SetFloat(POSITION_MORPH, StageMorph(elapsed, positionDelay, duration));
    }

    public static float GetDuration(float initialDelay, float duration)
    {
        return initialDelay + duration * (1 + POSITION_DELAY_FACTOR);
    }

    static float StageMorph(float elapsed, float delay, float duration)
    {
        return Mathf.Clamp01((elapsed - delay) / duration);
    }
}
}
