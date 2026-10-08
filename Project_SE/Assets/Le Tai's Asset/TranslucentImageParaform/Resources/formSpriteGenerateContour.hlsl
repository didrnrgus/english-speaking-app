#ifndef FORM_SPRITE_GENERATE_CONTOUR_INCLUDED
#define FORM_SPRITE_GENERATE_CONTOUR_INCLUDED

#include "formSpriteGenerateCommon.hlsl"

#define EDGE_INSET (1.0 / 2048.0)
float loadSourceAlpha(int2 coord)
{
    bool inBounds = all(coord >= 0) && all(coord < int2(_MainTex_TexelSize.zw));
    return inBounds ? _MainTex.Load(int3(coord, 0)).a : 0;
}

bool isSaddle(int2 cell)
{
    bool s00 = loadSourceAlpha(cell) > _AlphaThreshold;
    bool s10 = loadSourceAlpha(cell + int2(1, 0)) > _AlphaThreshold;
    bool s01 = loadSourceAlpha(cell + int2(0, 1)) > _AlphaThreshold;
    bool s11 = loadSourceAlpha(cell + int2(1, 1)) > _AlphaThreshold;

    return s00 == s11 && s10 == s01 && s00 != s10;
}

float alphaPreprocessFrag(v2f i) : SV_Target
{
    int2  coord = int2(i.pos.xy);
    float alpha = loadSourceAlpha(coord);
    if (alpha > _AlphaThreshold)
        return alpha;

    bool thicken = false;
    UNITY_UNROLL
    for (int y = -1; y <= 0; y++)
    {
        UNITY_UNROLL
        for (int x  = -1; x <= 0; x++)
            thicken = thicken || isSaddle(coord + int2(x, y));
    }

    return thicken ? _AlphaThreshold + .001 : alpha;
}

float insetCardinalEdge(float value)
{
    bool diagonalConnection = value <= 0 || value >= 1;
    return diagonalConnection ? value : clamp(value, EDGE_INSET, 1 - EDGE_INSET);
}

float4 contourFrag(v2f i) : SV_Target
{
    float2 center       = i.pos.xy - 0.5;
    float4 g            = _MainTex.GatherRed(sampler_MainTex, center * _MainTex_TexelSize.xy);
    bool4  axisInBounds = bool4(center >= 1, center < _MainTex_TexelSize.zw);
    bool4  isInBounds   = axisInBounds.xzxz && axisInBounds.yyww;

    float a00 = isInBounds.x ? g.w : 0;
    float a10 = isInBounds.y ? g.z : 0;
    float a01 = isInBounds.z ? g.x : 0;
    float a11 = isInBounds.w ? g.y : 0;

    float thr = _AlphaThreshold;

    float v00 = a00 - thr;
    float v10 = a10 - thr;
    float v01 = a01 - thr;
    float v11 = a11 - thr;

    float cX0 = v00 * v10 <= 0 ? 1 : 0;
    float cY1 = v10 * v11 <= 0 ? 1 : 0;
    float cX1 = v01 * v11 <= 0 ? 1 : 0;
    float cY0 = v00 * v01 <= 0 ? 1 : 0;

    float count = cX0 + cY1 + cX1 + cY0;

    if (count != 2)
        return 0;

    float dX0 = a10 - a00;
    float dY1 = a11 - a10;
    float dX1 = a11 - a01;
    float dY0 = a01 - a00;

    float tX0 = saturate(-v00 / (dX0 + (dX0 >= 0 ? EPS : -EPS)));
    float tY1 = saturate(-v10 / (dY1 + (dY1 >= 0 ? EPS : -EPS)));
    float tX1 = saturate(-v01 / (dX1 + (dX1 >= 0 ? EPS : -EPS)));
    float tY0 = saturate(-v00 / (dY0 + (dY0 >= 0 ? EPS : -EPS)));
    tX0       = insetCardinalEdge(tX0);
    tY1       = insetCardinalEdge(tY1);
    tX1       = insetCardinalEdge(tX1);
    tY0       = insetCardinalEdge(tY0);

    float2 pX0 = float2(tX0, 0);
    float2 pY1 = float2(1, tY1);
    float2 pX1 = float2(tX1, 1);
    float2 pY0 = float2(0, tY0);

    float2 pa = cX0 > 0 ? pX0 : (cY1 > 0 ? pY1 : pX1);
    float2 pb = cY0 > 0 ? pY0 : (cX1 > 0 ? pX1 : pY1);

    float s00 = a00 > thr ? 1 : 0;
    float s10 = a10 > thr ? 1 : 0;
    float s01 = a01 > thr ? 1 : 0;
    float s11 = a11 > thr ? 1 : 0;

    float2 insideCenter = float2(s10 + s11, s01 + s11) / max(s00 + s10 + s01 + s11, 1.0);

    float4 seg = cross2(pb - pa, insideCenter - pa) >= 0 ? float4(pa, pb) : float4(pb, pa);

    return seg;
}

float _SmoothFactor;

float isectRayVerticalWall(float2 origin, float2 dir, float wallX)
{
    float t = abs(dir.x) > EPS ? (wallX - origin.x) / dir.x : 0;
    return origin.y + t * dir.y;
}

float2 smoothVertex(float2 coord, float2 vertCenter, float2 vertPrev)
{
    int2   neighborDir = getNeighborDir(vertCenter);
    float4 neighborSeg = _MainTex.Load(int3(coord + neighborDir, 0));
    float2 vertNext    = nextVertex(neighborSeg, neighborDir);

    float2 prevToNext = vertNext - vertPrev;

    bool wallIsH = neighborDir.y != 0;

    // Rotate so the shared wall is always vertical
    float2 originRot       = wallIsH ? vertPrev.yx : vertPrev.xy;
    float2 dirRot          = wallIsH ? prevToNext.yx : prevToNext.xy;
    float  wallRotX        = any(neighborDir < 0) ? 0 : 1;
    float  targetAlongWall = isectRayVerticalWall(originRot, dirRot, wallRotX);
    targetAlongWall        = max(1e-2, min(1 - 1e-2, targetAlongWall));

    float centerAlongWall   = wallIsH ? vertCenter.x : vertCenter.y;
    float smoothedAlongWall = (lerp(centerAlongWall, targetAlongWall, _SmoothFactor));

    float2 toPrev           = normalize(vertPrev - vertCenter);
    float2 toNext           = normalize(vertNext - vertCenter);
    smoothedAlongWall       = dot(toNext, toPrev) < -.8 ? smoothedAlongWall : centerAlongWall;
    bool diagonalConnection = all(neighborDir != 0);
    smoothedAlongWall       = diagonalConnection ? smoothedAlongWall : clamp(smoothedAlongWall, EDGE_INSET, 1 - EDGE_INSET);

    // Rotate back
    return wallIsH ? float2(smoothedAlongWall, wallRotX) : float2(wallRotX, smoothedAlongWall);
}

float4 contourSmoothFrag(v2f i) : SV_Target
{
    float4 seg = _MainTex.Load(int3(i.pos.xy, 0));

    if (all(seg == 0))
        return seg;

    float2 pa = smoothVertex(i.pos.xy, seg.xy, seg.zw);
    float2 pb = smoothVertex(i.pos.xy, seg.zw, seg.xy);

    return float4(pa, pb);
}

#endif
