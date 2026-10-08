// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;
using UnityEngine.Sprites;

namespace LeTai.Paraform
{
public static class FormAssetExtensions
{
    internal static Vector4 GetContentUV(this FormAsset form)
    {
        var uv          = DataUtility.GetOuterUV(form.FormSprite);
        var textureSize = form.TextureSize;
        var contentSize = form.ContentSize;
        var insetX      = (textureSize.x - contentSize.x) / (2f * textureSize.x) * (uv.z - uv.x);
        var insetY      = (textureSize.y - contentSize.y) / (2f * textureSize.y) * (uv.w - uv.y);
        return new Vector4(uv.x + insetX, uv.y + insetY, uv.z - insetX, uv.w - insetY);
    }
}
}
