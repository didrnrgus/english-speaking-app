#ifndef FORM_SPRITE_GENERATE_COMMON_INCLUDED
#define FORM_SPRITE_GENERATE_COMMON_INCLUDED

#include "UnityCG.cginc"

#define FLOOD_NULL_POS (-1e9)
#define FLOOD_NULL_POS_FLOAT2 float2(FLOOD_NULL_POS, FLOOD_NULL_POS)

#define EPS 1e-6
#define CORNER_PROBE_DIST 1.41

Texture2D    _MainTex;
SamplerState sampler_MainTex;
float4       _MainTex_TexelSize;
Texture2D    _Contour;
float4       _Contour_TexelSize;
float2       _TargetSize;
float2       _TargetOffset;

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 pos : SV_POSITION;
};

v2f vert(appdata v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv  = v.uv;
    return o;
}

float cross2(float2 a, float2 b)
{
    return a.x * b.y - a.y * b.x;
}

float2 closestPointOnSegment(float2 p, float2 a, float2 b)
{
    float2 ap = p - a;
    float2 ab = b - a;
    float  t  = saturate(dot(ap, ab) / dot(ab, ab));
    return a + t * ab;
}

int2 getNeighborDir(float2 wallPoint)
{
    return (wallPoint > 1 - EPS) ? 1 : (wallPoint < EPS ? -1 : 0);
}

float2 nextVertex(float4 neighborSeg, int2 neighborDir)
{
    // If we stepped in the +1 dir, the neighbor's shared wall is at 0 along the axis, and vice versa
    float sharedWallCoord = any(neighborDir > 0) ? 0 : 1;
    bool  wallAxisIsX     = neighborDir.x != 0;

    float2 pa = neighborSeg.xy;
    float2 pb = neighborSeg.zw;

    float paOnWallAxis = wallAxisIsX ? pa.x : pa.y;
    float pbOnWallAxis = wallAxisIsX ? pb.x : pb.y;

    bool paIsShared = abs(paOnWallAxis - sharedWallCoord) < EPS;
    bool pbIsShared = abs(pbOnWallAxis - sharedWallCoord) < EPS;

    float2 farVertex = paIsShared ? pb : pa;
    farVertex        = paIsShared == pbIsShared ? pb : farVertex;

    return float2(neighborDir) + farVertex;
}

float2 contourToWorld(float2 cell, float2 localPoint)
{
    static float2 scale = _TargetSize / (_Contour_TexelSize.zw - 1);
    return (cell + localPoint - .5) * scale + _TargetOffset;
}

float2 worldToContour(float2 worldPos)
{
    static float2 scale = _TargetSize / (_Contour_TexelSize.zw - 1);
    return (worldPos - _TargetOffset) / scale + .5;
}

#endif
