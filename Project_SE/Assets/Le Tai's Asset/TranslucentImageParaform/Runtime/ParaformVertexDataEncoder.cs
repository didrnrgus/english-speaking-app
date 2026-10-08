// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Common;
using LeTai.Paraform.Scaffold;
using UnityEngine;
using static UnityEngine.Mathf;


namespace LeTai.Paraform
{
public readonly struct ParaformVertexDataEncoder
{
    public ref struct PackedBoundary
    {
        public Vec4Part2           vertexP;
        public Vec4Part1           elementP;
        public Packing.FloatPacker f18;
        public Packing.FloatPacker f15;
    }

    enum EffectKind : uint
    {
        Shadow  = 1,
        Outline = 2
    }

    readonly ParaformConfig _config;
    readonly Vector2        _extent;
    readonly Vector2        _origin;
    readonly float          _maxRealRadius;
    readonly Vector4        _realRadii;
    readonly Vector4        _apparentRadii;
    readonly float          _effectiveCornerPower;
    readonly float          _effectiveBevelWidth;
    readonly float          _effectiveHalfRingThickness;
    readonly float          _effectiveDistanceRange;
    readonly float          _effectWidthRange;

    public ParaformVertexDataEncoder(Rect rect, ParaformConfig config)
    {
        _config = config;

        _extent = rect.size / 2f;
        _origin = rect.center;

        _effectiveDistanceRange = config.FormSprite
            ? config.FormAsset.generationParams.distanceRange * ParaformUtils.GetFormPixelScale(config.FormAsset, rect)
            : 0;
        _effectiveHalfRingThickness = ParaformUtils.GetHalfRingThickness(config, rect);

        (_realRadii, _apparentRadii, _effectiveCornerPower, _maxRealRadius) =
            ParaformUtils.NormalizeShape(config.CornerRadii, _extent, config.CornerCurvature);

        if (config.FormMode == FormMode.Asset)
        {
            _effectWidthRange    = Max(_effectiveDistanceRange, 1e-6f);
            _effectiveBevelWidth = Min(config.BevelWidth, _effectiveDistanceRange);
        }
        else
        {
            _effectWidthRange = _maxRealRadius;
            var minRadius = Min(Min(Min(_realRadii.x, _realRadii.y), _realRadii.z), _realRadii.w);
            _effectiveBevelWidth = Min(config.BevelWidth, minRadius);
        }

        if (_effectiveHalfRingThickness > 0)
            _effectiveBevelWidth = Min(_effectiveBevelWidth, _effectiveHalfRingThickness);
        _effectiveBevelWidth = Max(1e-6f, _effectiveBevelWidth);
    }

    public Vector4 PackVertex(Vec4Part2 quad, Vector2 vertexPosition)
    {
        return quad
              .Append(vertexPosition.x - _origin.x)
              .Append(vertexPosition.y - _origin.y);
    }

    public PackedBoundary PackBoundary(bool useApparentRadii = false)
    {
        var radii = useApparentRadii ? _apparentRadii : _realRadii;
        return new PackedBoundary {
            vertexP = Vec4Builder.Append(_extent.x).Append(_extent.y),
            elementP = Vec4Builder.Append(_config.FormMode == FormMode.Asset
                                              ? -_effectiveDistanceRange
                                              : Packing.FloatPacker.Uniform(10)
                                                       .Enqueue(radii.x, _maxRealRadius)
                                                       .Enqueue(radii.y, _maxRealRadius)
                                                       .Enqueue(radii.z, _maxRealRadius)),
            f18 = Packing.FloatPacker.Varying()
                         .Enqueue(radii.w,               0,      _maxRealRadius, 10)
                         .Enqueue(_effectiveCornerPower, 1.001f, 7,              8),
            f15 = Packing.FloatPacker.Uniform(15)
                         .Enqueue(_effectiveHalfRingThickness, _effectWidthRange)
        };
    }

    public (Vec4Part2, Vector4) PackSurface(PackedBoundary boundary)
    {
        return (
            boundary.vertexP,
            boundary.elementP
                    .Append(boundary.f18.Enqueue(_config.FilletCurvature + 1, 1.001f, 7, 8))
                    .Append(boundary.f15)
                    .Append(Packing.FloatPacker.Uniform(15)
                                   .Enqueue(_effectiveBevelWidth, _effectWidthRange)
                                   .Enqueue(_config.Elevation,    1000))
        );
    }

    public (Vec4Part2, Vector4) PackShadow(
        PackedBoundary boundary,
        float          spread,
        float          softness,
        bool           useShadowMorph,
        SoftnessType   softnessType
    )
    {
        var range = Max(_extent.x, _extent.y);
        return (
            boundary.vertexP,
            boundary.elementP
                    .Append(boundary.f18.WithSign(true).Enqueue(spread, -range, range, 12))
                    .Append(boundary.f15)
                    .Append(Packing.FloatPacker.Varying()
                                   .Enqueue((uint)EffectKind.Shadow | ((uint)softnessType << 2), 0, 15,    4)
                                   .Enqueue(softness,                                            0, range, 12)
                                   .Enqueue(useShadowMorph ? 1f : 0f,                            0, 1,     1))
        );
    }

    public (Vec4Part2, Vector4) PackOutline(
        PackedBoundary boundary,
        float          innerWidth,
        float          outerWidth,
        float          innerSoftness,
        float          outerSoftness,
        SoftnessType   softnessType
    )
    {
        var range = Max(_extent.x, _extent.y);
        return (
            boundary.vertexP,
            boundary.elementP
                    .Append(boundary.f18.WithSign(true).Enqueue(innerWidth, -range, range, 12))
                    .Append(boundary.f15.Enqueue(outerSoftness, 0, range, 12))
                    .Append(Packing.FloatPacker.Varying()
                                   .Enqueue((uint)EffectKind.Outline | ((uint)softnessType << 2), 0,      15,    4)
                                   .Enqueue(outerWidth,                                           -range, range, 12)
                                   .Enqueue(innerSoftness,                                        0,      range, 12))
        );
    }
}
}
