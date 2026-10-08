// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Paraform.Scaffold;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace LeTai.Paraform
{
public static class ParaformUtils
{
    static readonly float CornerInset = 1f - Mathf.Sqrt(.5f);

    public static Rect GetRefractionRect(in ParaformConfig config, Rect rect, Vector4 refractiveIndexRatios)
    {
        var eta    = Mathf.Min(refractiveIndexRatios.x, Mathf.Min(refractiveIndexRatios.y, refractiveIndexRatios.z));
        var offset = GetMaxRefractionOffset(config.Elevation, eta);

        float padX;
        float padY;

        var minExtent     = Mathf.Min(rect.width, rect.height) / 2f;
        var ringThickness = Mathf.Max(0, Mathf.Min(config.RingThickness, minExtent));
        if (ringThickness == 0 || ringThickness > minExtent)
        {
            padX = Mathf.Max(0, offset - rect.width);
            padY = Mathf.Max(0, offset - rect.height);
        }
        else
        {
            padX = Mathf.Max(0, offset - ringThickness);
            padY = Mathf.Max(0, offset - ringThickness);
        }

        rect.x      -= padX / 2;
        rect.y      -= padY / 2;
        rect.width  += padX;
        rect.height += padY;
        return rect;
    }

    static float GetMaxRefractionOffset(float minDistance, float eta)
    {
        float k      = 1.0f - eta * eta;
        float offset = Mathf.Sqrt(k) * minDistance / eta;
        return offset;
    }

    public static bool TryPopulateFormSpriteMesh(
        in ParaformConfig config,
        Rect              rect,
        Vector2           pivot,
        Color32           color,
        VertexHelper      destination,
        out Rect          drawingRect
    )
    {
        drawingRect = default;
        var form = config.FormAsset;
        if (!config.FormSprite)
            return false;

        drawingRect = GetFormSpriteDrawingRect(rect, form, pivot);

        var uv = form.GetContentUV();
        destination.Clear();
        destination.AddVert(new Vector3(drawingRect.xMin, drawingRect.yMin), color, new Vector2(uv.x, uv.y));
        destination.AddVert(new Vector3(drawingRect.xMin, drawingRect.yMax), color, new Vector2(uv.x, uv.w));
        destination.AddVert(new Vector3(drawingRect.xMax, drawingRect.yMax), color, new Vector2(uv.z, uv.w));
        destination.AddVert(new Vector3(drawingRect.xMax, drawingRect.yMin), color, new Vector2(uv.z, uv.y));
        destination.AddTriangle(0, 1, 2);
        destination.AddTriangle(2, 3, 0);
        return true;
    }

    internal static (Vector4 realRadii, Vector4 apparentRadii, float cornerPower, float maxRealRadius)
        NormalizeShape(Vector4 apparentRadii, Vector2 extent, float cornerCurvature)
    {
        float maxRealRadius = Mathf.Min(extent.x, extent.y);

        float largestRadius = Mathf.Max(Mathf.Max(Mathf.Max(apparentRadii.x, apparentRadii.y), apparentRadii.z), apparentRadii.w);

        float radiusCoverage = Mathf.Min(1, largestRadius / maxRealRadius);
        float termInLog      = 1f - radiusCoverage * CornerInset;
        float powerCap       = Mathf.Max(2, Mathf.Log(.5f, termInLog));

        float cornerPower         = Mathf.Min(powerCap, cornerCurvature + 1);
        float apparentToRealRatio = ApparentToRealRadiusRatio(cornerPower);
        float maxApparentRadius   = maxRealRadius / apparentToRealRatio;
        var effectiveApparentRadii = new Vector4(Mathf.Min(maxApparentRadius, apparentRadii.x),
                                                 Mathf.Min(maxApparentRadius, apparentRadii.y),
                                                 Mathf.Min(maxApparentRadius, apparentRadii.z),
                                                 Mathf.Min(maxApparentRadius, apparentRadii.w));

        return (effectiveApparentRadii * apparentToRealRatio, effectiveApparentRadii, cornerPower, maxRealRadius);
    }

    static float ApparentToRealRadiusRatio(float cornerPower)
    {
        return cornerPower < 2f ? 1f : CornerInset / (1f - Mathf.Pow(2f, -1f / cornerPower));
    }

    public static bool IsRaycastLocationValid(ParaformConfig config, Rect rect, Vector2 localPoint, Vector2 pivot, Vector4 padding)
    {
        var dilation = 0f;
        if (config.FormMode == FormMode.Asset)
        {
            var formSprite = config.FormSprite;
            if (!config.UsePreciseHitTest || !formSprite || !formSprite.texture || !formSprite.texture.isReadable)
                return true;

            rect = GetFormSpriteDrawingRect(rect, config.FormAsset, pivot);

            const float band = .25f;

            var position = Rect.PointToNormalized(rect, localPoint) * 2f - Vector2.one;
            var blend    = (Mathf.Abs(position.x) - Mathf.Abs(position.y) + band) / (2f * band);
            dilation = -Mathf.Lerp(position.y >= 0 ? padding.w : padding.y,
                                   position.x >= 0 ? padding.z : padding.x, blend);

            var distanceRange = config.FormAsset.generationParams.distanceRange * GetFormPixelScale(config.FormAsset, rect);
            dilation = Mathf.Clamp(dilation, -distanceRange, distanceRange);
        }
        else
        {
            rect.xMin += padding.x;
            rect.yMin += padding.y;
            rect.xMax -= padding.z;
            rect.yMax -= padding.w;
        }

        var edgeDistance = config.FormMode == FormMode.Asset
            ? DistFormSprite(config, rect, localPoint)
            : DistRoundedBox(localPoint - rect.center, rect.size, config.CornerRadii, config.CornerCurvature);
        var halfRingThickness = GetHalfRingThickness(config, rect);
        if (halfRingThickness > 0)
            edgeDistance = Mathf.Abs(edgeDistance + halfRingThickness) - halfRingThickness;
        edgeDistance -= dilation;
        return edgeDistance <= 0;
    }

    public static float DistFormSprite(ParaformConfig config, Rect rect, Vector2 localPoint)
    {
        if (!rect.Contains(localPoint))
            return float.PositiveInfinity;

        var formSprite = config.FormSprite;
        var localUv    = Rect.PointToNormalized(rect, localPoint);
        var outerUv    = config.FormAsset.GetContentUV();
        var uv = new Vector2(
            Mathf.Lerp(outerUv.x, outerUv.z, localUv.x),
            Mathf.Lerp(outerUv.y, outerUv.w, localUv.y)
        );
        var texture = formSprite.texture;
        var encodedDistance = texture.GetPixelBilinear(uv.x - .5f / texture.width,
                                                       uv.y - .5f / texture.height);
        var distanceRange = config.FormAsset.generationParams.distanceRange * GetFormPixelScale(config.FormAsset, rect);
        return DecodeDistance(new Vector2(encodedDistance.b, encodedDistance.a), distanceRange);
    }

    public static Rect GetFormSpriteDrawingRect(Rect rect, FormAsset form, Vector2 pivot)
    {
        var contentSize = form.ContentSize;
        var fittedSize  = (Vector2)contentSize * Mathf.Min(GetFormPixelScale(form, rect), rect.height / contentSize.y);
        rect.position += Vector2.Scale(rect.size - fittedSize, pivot);
        rect.size     =  fittedSize;

        var padding      = DataUtility.GetPadding(form.FormSprite);
        var textureSize  = form.TextureSize;
        var paddingScale = new Vector2(rect.width / textureSize.x, rect.height / textureSize.y);
        return Rect.MinMaxRect(rect.xMin + padding.x * paddingScale.x,
                               rect.yMin + padding.y * paddingScale.y,
                               rect.xMax - padding.z * paddingScale.x,
                               rect.yMax - padding.w * paddingScale.y);
    }

    public static float GetFormPixelScale(FormAsset form, Rect rect)
    {
        return rect.width / form.ContentSize.x;
    }

    static float DecodeDistance(Vector2 encoded, float distanceRange)
    {
        var distance = encoded.x + encoded.y / 255f;
        return (distance * 2 - 1) * distanceRange;
    }

    public static float DistRoundedBox(Vector2 localPosition, Vector2 size, Vector4 radii, float cornerCurvature)
    {
        var b = size / 2f;
        var (r, _, n, _) = NormalizeShape(radii, b, cornerCurvature);

        r = new Vector4(
            (localPosition.x > 0) ? r.x : r.z,
            (localPosition.x > 0) ? r.y : r.w,
            r.z,
            r.w
        );
        var radius = (localPosition.y > 0) ? r.x : r.y;

        var q            = new Vector2(Mathf.Abs(localPosition.x), Mathf.Abs(localPosition.y)) - b + Vector2.one * radius;
        var q0           = Vector2.Max(q, Vector2.zero);
        var m            = Mathf.Pow(q0.x, n) + Mathf.Pow(q0.y, n);
        var q0norm       = Mathf.Pow(m, 1.0f / n);
        var edgeDistance = Mathf.Min(Mathf.Max(q.x, q.y), 0) + q0norm - radius;

        return edgeDistance;
    }

    public static float GetHalfRingThickness(ParaformConfig config, Rect rect)
    {
        var effectWidthRange = Mathf.Min(rect.width, rect.height) / 2;
        if (config.FormMode == FormMode.Asset)
            effectWidthRange = config.FormSprite
                ? config.FormAsset.generationParams.distanceRange * GetFormPixelScale(config.FormAsset, rect)
                : 0;
        return config.RingThickness <= effectWidthRange * 2 ? config.RingThickness / 2 : 0;
    }
}
}
