// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using LeTai.Common;
using LeTai.Paraform.Scaffold;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Paraform
{
public static class ParaformEffectMesh
{
    const float GAUSSIAN_SIGMA_RANGE = 3;

    ref struct Element
    {
        public Rect                      rect;
        public ParaformVertexDataEncoder encoder;
        public Vector2                   offset;
        public Color                     color;
        public float                     expansion;
        public Vector4                   uv0;
        public Vec4Part2                 uv2Common;
        public Vector4                   uv3;
    }

    static readonly List<UIVertex> FILL_STREAM      = new();
    static readonly List<Effect>   UNDERLAY_EFFECTS = new();
    static readonly List<Effect>   OVERLAY_EFFECTS  = new();

    public static void Populate(
        VertexHelper              vh,
        Rect                      rect,
        in ParaformConfig         config,
        ParaformVertexDataEncoder encoder,
        IReadOnlyList<Effect>     effects,
        float                     pixelSize,
        Color                     graphicColor
    )
    {
        UNDERLAY_EFFECTS.Clear();
        OVERLAY_EFFECTS.Clear();
        if (effects.Count == 0)
            return;

        for (var i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null || !effect.enabled)
                continue;

            switch (effect.Placement)
            {
            case EffectPlacement.Underlay:
                UNDERLAY_EFFECTS.Add(effect);
                break;
            case EffectPlacement.Overlay:
                OVERLAY_EFFECTS.Add(effect);
                break;
            }
        }

        if (UNDERLAY_EFFECTS.Count > 0)
        {
            FILL_STREAM.Clear();
            vh.GetUIVertexStream(FILL_STREAM);
            vh.Clear();

            AddEffects(vh, rect, in config, encoder, UNDERLAY_EFFECTS, pixelSize, graphicColor);
            AddTriangleStream(vh, FILL_STREAM);
            FILL_STREAM.Clear();
        }

        if (OVERLAY_EFFECTS.Count > 0)
            AddEffects(vh, rect, in config, encoder, OVERLAY_EFFECTS, pixelSize, graphicColor);

        UNDERLAY_EFFECTS.Clear();
        OVERLAY_EFFECTS.Clear();
    }

    static void AddEffects(
        VertexHelper              vh,
        Rect                      rect,
        in ParaformConfig         config,
        ParaformVertexDataEncoder encoder,
        List<Effect>              effects,
        float                     pixelSize,
        Color                     graphicColor
    )
    {
        var formSprite = config.FormSprite;
        var contentUv = formSprite
            ? config.FormAsset.GetContentUV()
            : new Vector4(0, 0, 1, 1);
        var textureExpansionLimit = float.PositiveInfinity;
        if (formSprite)
        {
            var form        = config.FormAsset;
            var contentSize = form.ContentSize;
            var textureSize = form.TextureSize;
            var border      = Mathf.Min(textureSize.x - contentSize.x, textureSize.y - contentSize.y) * .5f;
            textureExpansionLimit = border * ParaformUtils.GetFormPixelScale(form, rect);
        }
        for (var i = 0; i < effects.Count; i++)
        {
            var     effect = effects[i];
            Element element;
            switch (effect)
            {
            case Scaffold.Shadow shadow:
                element = CreateShadowElement(rect, encoder, in config, shadow);
                break;
            case Scaffold.Outline outline:
                element = CreateOutlineElement(rect, encoder, in config, outline, pixelSize);
                break;
            default:
                continue;
            }

            element.color     = GetColor(effect, graphicColor);
            element.expansion = Mathf.Min(element.expansion, textureExpansionLimit);
            element.uv0 = config.FormMode == FormMode.Asset
                ? ExpandUv(contentUv, element.rect, element.expansion)
                : contentUv;
            AppendElement(vh, in element);
        }
    }

    static Element CreateShadowElement(Rect rect, ParaformVertexDataEncoder encoder, in ParaformConfig config, Scaffold.Shadow shadow)
    {
        var spread   = shadow.spread;
        var softness = shadow.softness;
        var useShadowMorph = config.FormMode == FormMode.RoundedBox &&
                             shadow.quality == Scaffold.ShadowQuality.High &&
                             softness > 0;
        if (useShadowMorph)
        {
            var sigma = Mathf.Max(softness / GAUSSIAN_SIGMA_RANGE, 1f / 16384f);
            softness = sigma * GAUSSIAN_SIGMA_RANGE;
            shadowMorph(ref rect, sigma);
            encoder = new ParaformVertexDataEncoder(rect, config);
        }

        if (config.FormMode == FormMode.Asset)
        {
            var range = config.FormSprite
                ? Mathf.Max(0, config.FormAsset.generationParams.distanceRange * ParaformUtils.GetFormPixelScale(config.FormAsset, rect))
                : 0;
            spread   = Mathf.Min(spread,   range);
            softness = Mathf.Min(softness, range - spread);
        }

        var boundary = encoder.PackBoundary(useShadowMorph);
        var (uv2Common, uv3) = encoder.PackShadow(boundary, spread, softness,
                                                  useShadowMorph, shadow.softnessType);
        return new Element {
            rect      = rect,
            encoder   = encoder,
            offset    = shadow.offset,
            expansion = spread + softness,
            uv2Common = uv2Common,
            uv3       = uv3
        };
    }

    static Element CreateOutlineElement(Rect rect, ParaformVertexDataEncoder encoder, in ParaformConfig config, Scaffold.Outline outline, float pixelSize)
    {
        var compensation  = .85f * Mathf.Sqrt(2f) * pixelSize;
        var edgeMargin    = compensation + pixelSize;
        var innerWidth    = outline.innerWidth;
        var outerWidth    = outline.outerWidth;
        var innerSoftness = outline.innerSoftness;
        var outerSoftness = outline.outerSoftness;
        if (config.FormMode == FormMode.Asset)
        {
            var range = config.FormSprite
                ? Mathf.Max(0, config.FormAsset.generationParams.distanceRange * ParaformUtils.GetFormPixelScale(config.FormAsset, rect))
                : 0;
            innerWidth    = Mathf.Min(innerWidth,    range);
            outerWidth    = Mathf.Min(outerWidth,    range);
            innerSoftness = Mathf.Min(innerSoftness, range - innerWidth);
            outerSoftness = Mathf.Min(outerSoftness, range - outerWidth);
        }

        var boundary = encoder.PackBoundary();
        var (uv2Common, uv3) = encoder.PackOutline(boundary, innerWidth, outerWidth,
                                                   innerSoftness, outerSoftness, outline.softnessType);
        return new Element {
            rect      = rect,
            encoder   = encoder,
            expansion = outerWidth + outerSoftness + edgeMargin,
            uv2Common = uv2Common,
            uv3       = uv3
        };
    }

    static Color GetColor(Effect effect, Color graphicColor)
    {
        var color = effect.color;
        if (effect.useGraphicColor)
        {
            color.r *= graphicColor.r;
            color.g *= graphicColor.g;
            color.b *= graphicColor.b;
        }
        if (effect.useGraphicAlpha)
            color.a *= graphicColor.a;
        return color;
    }

    static Vector4 ExpandUv(Vector4 uv, Rect rect, float expansion)
    {
        var insetX = expansion / rect.width * (uv.z - uv.x);
        var insetY = expansion / rect.height * (uv.w - uv.y);
        return new Vector4(uv.x - insetX, uv.y - insetY, uv.z + insetX, uv.w + insetY);
    }

    static void AppendElement(VertexHelper vh, in Element element)
    {
        var vertex = UIVertex.simpleVert;
        vertex.color = element.color;
        vertex.uv3   = element.uv3;

        var vertexStart = vh.currentVertCount;
        for (var corner = 0; corner < 4; corner++)
        {
            var right = corner >= 2;
            var top   = corner == 1 || corner == 2;
            var position = new Vector2(right ? element.rect.xMax + element.expansion : element.rect.xMin - element.expansion,
                                       top ? element.rect.yMax + element.expansion : element.rect.yMin - element.expansion);
            vertex.position = position + element.offset;
            vertex.uv0      = new Vector2(right ? element.uv0.z : element.uv0.x, top ? element.uv0.w : element.uv0.y);
            vertex.uv2      = element.encoder.PackVertex(element.uv2Common, position);
            vh.AddVert(vertex);
        }

        vh.AddTriangle(vertexStart,     vertexStart + 1, vertexStart + 2);
        vh.AddTriangle(vertexStart + 2, vertexStart + 3, vertexStart);
    }

    static void shadowMorph(ref Rect rect, float sigma)
    {
        var scale      = 1.4142135623730951f * sigma;
        var invScale   = .5f / scale;
        var width      = rect.width * invScale;
        var height     = rect.height * invScale;
        var widthTerm  = Mathf.Exp(-width * width);
        var heightTerm = Mathf.Exp(-height * height);
        var adjustment = 1.25f * scale * (widthTerm - heightTerm);
        var center     = rect.center;

        rect.width  += Mathf.Min(adjustment, 0);
        rect.height -= Mathf.Max(adjustment, 0);
        rect.center =  center;
    }

    static void AddTriangleStream(VertexHelper vh, List<UIVertex> stream)
    {
        for (var i = 0; i < stream.Count; i += 3)
        {
            var start = vh.currentVertCount;
            vh.AddVert(stream[i]);
            vh.AddVert(stream[i + 1]);
            vh.AddVert(stream[i + 2]);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
}
