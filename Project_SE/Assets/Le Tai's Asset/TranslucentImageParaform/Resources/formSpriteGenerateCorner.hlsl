#ifndef FORM_SPRITE_GENERATE_CORNER_INCLUDED
#define FORM_SPRITE_GENERATE_CORNER_INCLUDED


float _BendAngleCutoff;

struct WalkResult
{
    float2 pDelta;
    float2 pDelta2;
    bool   valid;
};

WalkResult walkDir(float2 startCell, float2 startWorld, float2 firstWorld, int2 firstDir, float delta)
{
    float2 cell      = startCell;
    float2 prevWorld = startWorld;
    float  cumLen    = 0;
    int2   nDir      = firstDir;

    float      doubleDelta = 2 * delta;
    bool       haveDelta   = false;
    WalkResult result      = (WalkResult)0;

    UNITY_LOOP
    for (int step = 0; step < 64; step++)
    {
        float2 nextCell     = cell;
        float2 nextPosLocal = 0;
        float2 nextWorld    = firstWorld;
        if (step > 0)
        {
            nextCell    = cell + float2(nDir);
            float4 nSeg = _Contour.Load(int3(nextCell, 0));
            if (all(nSeg == 0))
                break;

            nextPosLocal = nextVertex(nSeg, nDir) - float2(nDir);
            nextWorld    = contourToWorld(nextCell, nextPosLocal);
        }

        float segLen = distance(prevWorld, nextWorld);
        float newCum = cumLen + segLen;

        if (!haveDelta && newCum >= delta)
        {
            float fraction = segLen > EPS ? (delta - cumLen) / segLen : 0;
            result.pDelta  = lerp(prevWorld, nextWorld, fraction);
            haveDelta      = true;
        }

        if (newCum >= doubleDelta)
        {
            float fraction = segLen > EPS ? (doubleDelta - cumLen) / segLen : 0;
            result.pDelta2 = lerp(prevWorld, nextWorld, fraction);
            result.valid   = haveDelta;
            break;
        }

        cumLen    = newCum;
        prevWorld = nextWorld;
        if (step > 0)
        {
            cell = nextCell;
            nDir = getNeighborDir(nextPosLocal);
        }
    }

    return result;
}

void vertexTurn(float2 cell, float delta, out float t1, out float t2, out float errQuadOuter, out bool valid)
{
    t1           = 0;
    t2           = 0;
    errQuadOuter = 0;
    valid        = false;

    float4 seg = _Contour.Load(int3(cell, 0));
    if (all(seg == 0))
        return;

    float2 a = contourToWorld(cell, seg.xy);
    float2 b = contourToWorld(cell, seg.zw);
    float2 v = b;

    int2 dirA = getNeighborDir(seg.xy);
    int2 dirB = getNeighborDir(seg.zw);

    WalkResult back = walkDir(cell, b, a, dirA, delta);
    WalkResult fwd  = walkDir(cell, b, b, dirB, delta);

    if (!back.valid || !fwd.valid)
        return;

    float2 d1a = v - back.pDelta;
    float2 d1b = fwd.pDelta - v;
    float2 d2a = v - back.pDelta2;
    float2 d2b = fwd.pDelta2 - v;

    float2 accelBack   = back.pDelta2 - 2 * back.pDelta + v;
    float2 accelCenter = back.pDelta - 2 * v + fwd.pDelta;
    float2 accelFwd    = v - 2 * fwd.pDelta + fwd.pDelta2;

    t1           = atan2(cross2(d1a, d1b), dot(d1a, d1b));
    t2           = atan2(cross2(d2a, d2b), dot(d2a, d2b));
    errQuadOuter = max(distance(accelBack, accelCenter), distance(accelCenter, accelFwd));
    valid        = true;

    DBG_LINE(back.pDelta2, v, DBG_COL_ORANGE);
    DBG_LINE(v, fwd.pDelta2, DBG_COL_ORANGE);
    DBG_CIRCLE(back.pDelta2, .5, DBG_COL_ORANGE);
    DBG_CIRCLE(fwd.pDelta2, .5, DBG_COL_ORANGE);
    DBG_LINE(back.pDelta, v, DBG_COL_BROWN);
    DBG_LINE(v, fwd.pDelta, DBG_COL_BROWN);
    DBG_CIRCLE(back.pDelta, .5, DBG_COL_BROWN);
    DBG_CIRCLE(fwd.pDelta, .5, DBG_COL_BROWN);
}

float4 cornerDetectFrag(v2f i) : SV_Target
{
    float2 cell = floor(i.pos.xy);
    DBG_INIT(cell, _LeTai_DbgPixelContour.xy);

    float cellSize = _TargetSize.x / (_Contour_TexelSize.z - 1);
    float delta    = CORNER_PROBE_DIST * cellSize;

    float t1, t2, errQuadOuter;
    bool  valid;
    vertexTurn(cell, delta, t1, t2, errQuadOuter, valid);

    float cornerAngle = 2 * t1 - t2;

    bool isCorner =
            valid
            // && abs(cornerAngle) > _BendAngleCutoff
            && t1 * t2 > 0
            && abs(t1) > _BendAngleCutoff
            && cornerAngle * t1 > 0
        // && abs(t2) < 1.5 * abs(t1)
        ;

    return float4(isCorner ? 1 : 0, errQuadOuter / cellSize, t1 / CORNER_PROBE_DIST, 1);
}

#endif
