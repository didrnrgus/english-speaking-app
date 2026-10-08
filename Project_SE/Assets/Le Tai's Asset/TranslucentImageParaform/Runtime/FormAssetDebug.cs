// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Diagnostics;
using UnityEngine;

namespace LeTai.Paraform
{
public readonly struct Remap
{
    public readonly Vector4 scale;
    public readonly Vector4 bias;

    public static readonly Remap IDENTITY = new(Vector4.one, Vector4.one);

    public Remap(Vector4 fromMin, Vector4 fromMax, Vector4 toMin, Vector4 toMax)
    {
        var rangeFrom = fromMax - fromMin;
        var rangeTo   = toMax - toMin;

        scale = new Vector4(rangeTo.x / rangeFrom.x,
                            rangeTo.y / rangeFrom.y,
                            rangeTo.z / rangeFrom.z,
                            rangeTo.w / rangeFrom.w);
        bias = toMin - Vector4.Scale(fromMin, scale);
    }

    public Remap(Vector4 fromMax, Vector4 toMax)
        : this(Vector4.zero, fromMax, Vector4.zero, toMax) { }

    public Remap(Vector2 fromMin, Vector2 fromMax, Vector2 toMin, Vector2 toMax)
        : this(new Vector4(fromMin.x, fromMin.y, 0, 0),
               new Vector4(fromMax.x, fromMax.y, 1, 1),
               new Vector4(toMin.x,   toMin.y,   0, 0),
               new Vector4(toMax.x,   toMax.y,   1, 1)) { }

    public Remap(Vector2 fromMax, Vector2 toMax)
        : this(Vector2.zero, fromMax, Vector2.zero, toMax) { }

    public Vector2 Map(Vector2 value)
    {
        return Vector2.Scale(value, new Vector2(scale.x, scale.y)) + new Vector2(bias.x, bias.y);
    }
}

public static class FormAssetDebug
{
    public enum ViewMode
    {
        Color,
        Alpha,
        Red
    }

    public readonly struct GenerationScope : IDisposable
    {
        public void Dispose()
        {
#if LETAI_PARAFORM_DEBUG_INTERNAL
            ParaformShaderDebugger.EndGeneration();
#endif
        }
    }

    public static GenerationScope BeginGeneration()
    {
#if LETAI_PARAFORM_DEBUG_INTERNAL
        ParaformShaderDebugger.BeginGeneration();
#endif
        return default;
    }

    [Conditional("LETAI_PARAFORM_DEBUG_INTERNAL")]
    public static void UpdateSelectedCoordinates(Vector2 sourceSize, Vector2 targetSize, Vector2 targetOffset = default)
    {
#if LETAI_PARAFORM_DEBUG_INTERNAL
        ParaformShaderDebugger.UpdateSelectedCoordinates(sourceSize, targetSize, targetOffset);
#endif
    }

    [Conditional("LETAI_PARAFORM_DEBUG_INTERNAL")]
    public static void Capture(
        string   name,
        Texture  texture,
        Remap?   valRemap   = null,
        Remap?   coordRemap = null,
        ViewMode viewMode   = ViewMode.Color
    )
    {
#if LETAI_PARAFORM_DEBUG_INTERNAL
        ParaformShaderDebugger.Capture(name, texture, valRemap, coordRemap, viewMode);
#endif
    }

    [Conditional("LETAI_PARAFORM_DEBUG_INTERNAL")]
    public static void UseDrawLayer(string passName, int priority = 0)
    {
#if LETAI_PARAFORM_DEBUG_INTERNAL
        ParaformShaderDebugger.UseDrawLayer(passName, priority);
#endif
    }
}
}