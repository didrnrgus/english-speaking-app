#ifndef FORM_SPRITE_GENERATE_RESOLVE_INCLUDED
#define FORM_SPRITE_GENERATE_RESOLVE_INCLUDED

float _SigmaNear;
float _SigmaGrowth;
float _SigmaMax;
float _FitErrorCutoff;
float _BendAngleCutoff;
int   _ChainMaxSteps;

float getVertWeight(float arcLen, float sigma)
{
    float inv2sigma2 = 1.0 / (2.0 * sigma * sigma);
    return exp(-arcLen * arcLen * inv2sigma2);
}

struct Chain
{
    float2 nextPosLocal;
    float2 nextCell;
    float2 endPos;
    float2 endDir;
    float  cumArcLen;
    float  segMass;
    bool   stopped;
    bool   forward;
};

Chain chainInit(float2 posLocal, float2 cell, float2 endPos, float2 endDir, float arcLen, bool forward)
{
    Chain c;
    c.nextPosLocal = posLocal;
    c.nextCell     = cell;
    c.endPos       = endPos;
    c.endDir       = endDir;
    c.cumArcLen    = arcLen;
    c.segMass      = 0;
    c.stopped      = false;
    c.forward      = forward;
    return c;
}

bool chainTryAdvance(inout Chain chain, float walkRadius, float centerTurn)
{
    bool canAdvance = false;

    if (!chain.stopped) // compiler don't like early return ?
    {
        int2   nDir     = getNeighborDir(chain.nextPosLocal);
        float2 nextCell = chain.nextCell + float2(nDir);
        float4 nSeg     = _Contour.Load(int3(nextCell, 0));

        float2 cornerCell        = chain.forward ? chain.nextCell : nextCell;
        float4 vertexData        = _Corner.Load(int3(cornerCell, 0));
        float  reversalThreshold = _BendAngleCutoff / (2 * CORNER_PROBE_DIST);
        bool   turnReversal      = abs(centerTurn) > reversalThreshold
            && abs(vertexData.b) > reversalThreshold
            && centerTurn * vertexData.b < 0;

        if (all(nSeg == 0) || vertexData.r > 0.5 || vertexData.g > _FitErrorCutoff || turnReversal)
        {
            chain.stopped = true;
        }
        else
        {
            float2 nextPosLocal = nextVertex(nSeg, nDir) - float2(nDir);
            float2 nextPos      = contourToWorld(nextCell, nextPosLocal);
            float2 segA         = contourToWorld(nextCell, nSeg.xy);
            float2 segB         = contourToWorld(nextCell, nSeg.zw);

            float2 segVec = segB - segA;
            float  segLen = length(segVec);

            if (length(nSeg.zw - nSeg.xy) < 1e-4)
            {
                chain.nextPosLocal = nextPosLocal;
                chain.nextCell     = nextCell;
                chain.endPos       = nextPos;
            }
            else
            {
                float2 segDir = segVec / segLen;

                canAdvance = true;

                chain.nextPosLocal = nextPosLocal;
                chain.nextCell     = nextCell;
                chain.endPos       = nextPos;
                chain.endDir       = segDir;
                chain.cumArcLen += segLen;
                chain.segMass = segLen;

                chain.stopped = chain.cumArcLen >= walkRadius;
            }
        }
    }

    return canAdvance;
}

struct CurveFit
{
    float  M0, M1, M2, M3, M4;
    float2 B0, B1, B2;

    float tMin, tMax;
};

void curveFitAppend(inout CurveFit q, float2 pos, float t, float w)
{
    float  wt   = w * t;
    float2 wpos = w * pos;

    q.M0 += w;
    q.M1 += wt;
    q.B0 += wpos;

    float  wt2   = wt * t;
    float2 twpos = t * wpos;

    q.M2 += wt2;
    q.B1 += twpos;
    q.B2 += t * twpos;

    float wt3 = wt2 * t;
    q.M3 += wt3;
    q.M4 += wt3 * t;

    q.tMin = min(q.tMin, t);
    q.tMax = max(q.tMax, t);
}

void curveFitNormalizeSide(inout CurveFit q, float2 endpoint, float t)
{
    float emptyWeight = q.M0 > 0 ? 0 : 1;
    float scale       = rcp(q.M0 + emptyWeight);

    q.M0 *= scale;
    q.M1 *= scale;
    q.M2 *= scale;
    q.M3 *= scale;
    q.M4 *= scale;
    q.B0 *= scale;
    q.B1 *= scale;
    q.B2 *= scale;

    curveFitAppend(q, endpoint, t, emptyWeight);
}

CurveFit curveFitMerge(CurveFit a, CurveFit b)
{
    a.M0 += b.M0;
    a.M1 += b.M1;
    a.M2 += b.M2;
    a.M3 += b.M3;
    a.M4 += b.M4;
    a.B0 += b.B0;
    a.B1 += b.B1;
    a.B2 += b.B2;
    a.tMin = min(a.tMin, b.tMin);
    a.tMax = max(a.tMax, b.tMax);
    return a;
}

struct Curve
{
    float2 A,    B, C;
    float  tMin, tMax;
};

Curve curveSolve(CurveFit q)
{
    Curve curve;
    curve.tMin = q.tMin;
    curve.tMax = q.tMax;

    float tScale  = max(abs(q.tMin), abs(q.tMax));
    float tScale2 = tScale * tScale;
    float tScale4 = tScale2 * tScale2;
    float rel_eps = 1e-6;
    float eps1    = rel_eps * q.M0 * tScale2;

    float L11rcp = rsqrt(q.M0);
    float L21    = q.M1 * L11rcp;
    float L31    = q.M2 * L11rcp;

    float L22sq  = q.M2 - L21 * L21;
    float L22rcp = L22sq > eps1 ? rsqrt(L22sq) : 0;
    float L32    = (q.M3 - L31 * L21) * L22rcp;

    float L33sq = q.M4 - L31 * L31 - L32 * L32;
    float eps2  = rel_eps * q.M0 * tScale4;

    // prefer a straight line when curvature is underconstrained
    float ridgeLambda = 1e-3 * q.M0 * tScale4;
    float L33rcp      = L22sq > eps1 && L33sq > eps2 ? rsqrt(L33sq + ridgeLambda) : 0;

    float2 y1 = q.B0 * L11rcp;
    float2 y2 = (q.B1 - L21 * y1) * L22rcp;
    float2 y3 = (q.B2 - L31 * y1 - L32 * y2) * L33rcp;

    curve.C = y3 * L33rcp;
    curve.B = (y2 - L32 * curve.C) * L22rcp;
    curve.A = (y1 - L21 * curve.B - L31 * curve.C) * L11rcp;

    return curve;
}

float2 curveEval(Curve curve, float t)
{
    return curve.A + curve.B * t + curve.C * t * t;
}

float2 curveDeriv(Curve curve, float t)
{
    return curve.B + 2 * curve.C * t;
}

float curveClosestTNewton(float2 p, Curve curve)
{
    static float cloestTIters = 4; // gentle curve guaranteed
    float        t            = 0;
    for (int j = 0; j < cloestTIters; j++)
    {
        float2 pt   = curveEval(curve, t);
        float2 dpt  = curveDeriv(curve, t);
        float2 diff = pt - p;
        float  num  = dot(diff, dpt);
        float  den  = dot(dpt, dpt) + dot(diff, 2 * curve.C);
        t -= num / (den + EPS);
        t = clamp(t, curve.tMin, curve.tMax);
        // DBG_CIRCLE(pt, 1, DbgFade(DBG_COL_PINK, .4));
    }
    return t;
}

float curveClosestT(float2 pos, Curve curve)
{
    float2 D       = curve.A - pos;
    float  bb      = dot(curve.B, curve.B);
    float  cc      = dot(curve.C, curve.C);
    float  tRange  = max(abs(curve.tMin), abs(curve.tMax));
    float  tRange2 = tRange * tRange;

    float  linearRange2 = bb * tRange2;
    float  quadRange2   = cc * tRange2 * tRange2;
    float  lineT        = bb > 0 ? clamp(-dot(D, curve.B) / bb, curve.tMin, curve.tMax) : curve.tMin;
    float2 lineDiff     = curveEval(curve, lineT) - pos;

    static const float LINEAR_REL_EPS2 = 2.3841858e-7;
    static const float POINT_ABS_EPS2  = 1e-8;

    if (quadRange2 <= linearRange2 * LINEAR_REL_EPS2)
        return linearRange2 <= POINT_ABS_EPS2 ? curve.tMin : lineT;

    float2 B    = curve.B * tRange;
    float2 C    = curve.C * tRange2;
    float  uMin = curve.tMin / tRange;
    float  uMax = curve.tMax / tRange;

    cc       = dot(C, C);
    float ba = 0.5 / cc;

    float kx = dot(B, C) * ba;
    float ky = (dot(B, B) + 2 * dot(D, C)) * ba / 3;
    float kz = dot(D, B) * ba;

    float p          = ky - kx * kx;
    float q          = kx * (2 * kx * kx - 3 * ky) + kz;
    float q2         = q * q;
    float neg4p3     = -4 * p * p * p;
    float h          = q2 - neg4p3;
    bool  threeRoots = p < 0 && q2 < neg4p3;

    if (!threeRoots)
    {
        float  s          = sqrt(max(h, 0));
        float  x          = -0.5 * (q + (q >= 0 ? s : -s));
        float  root       = sign(x) * pow(abs(x), 1.0 / 3.0);
        float  otherRoot  = root == 0 ? 0 : -p / root;
        float  u          = clamp(root + otherRoot - kx, uMin, uMax);
        float  resultT    = u * tRange;
        float2 resultDiff = curveEval(curve, resultT) - pos;
        return dot(resultDiff, resultDiff) < dot(lineDiff, lineDiff) ? resultT : lineT;
    }

    float z = sqrt(-p);
    float v = acos(clamp(-q / sqrt(neg4p3), -1, 1)) / 3;
    float m = cos(v);
    float n = sin(v) * 1.7320508075688772;

    float2 u = clamp(float2(2 * m, -n - m) * z - kx, uMin, uMax);

    float2 r0 = D + (B + C * u.x) * u.x;
    float2 r1 = D + (B + C * u.y) * u.y;
    float  d0 = dot(r0, r0);
    float  d1 = dot(r1, r1);

    float resultT     = (d0 < d1 ? u.x : u.y) * tRange;
    float resultDist2 = min(d0, d1);
    return resultDist2 < dot(lineDiff, lineDiff) ? resultT : lineT;
}

#if defined(LETAI_DBG)
void dbgDrawContourWindow(float2 nearestCell)
{
    int2 lo = int2(0, 0);
    int2 hi = int2(_Contour_TexelSize.zw) - 1;
    if (_LeTai_DbgGridRadius >= 0)
    {
        lo = max(lo, int2(nearestCell) - _LeTai_DbgGridRadius);
        hi = min(hi, int2(nearestCell) + _LeTai_DbgGridRadius);
    }

        UNITY_LOOP
    for (int cy = lo.y; cy <= hi.y; cy++)
    {
            UNITY_LOOP
        for (int cx = lo.x; cx <= hi.x; cx++)
        {
            float4 seg = _Contour.Load(int3(cx, cy, 0));
            if (all(seg == 0))
                continue;

            float2 cell    = float2(cx, cy);
            float2 w00     = contourToWorld(cell, float2(0, 0));
            float2 w11     = contourToWorld(cell, float2(1, 1));
            uint   wallCol = DbgFade(DBG_COL_GRAY, .1);
            DBG_BOX(w00, w11, wallCol);

            bool isNearest = all(int2(nearestCell) == int2(cx, cy));
            DBG_LINE(contourToWorld(cell, seg.xy), contourToWorld(cell, seg.zw),
           isNearest ? DBG_COL_GREEN : DbgFade(DBG_COL_GRAY, .5));
        }
    }
}
#endif

half4 frag(v2f i) : SV_Target
{
    DBG_INIT(i.pos.xy, _LeTai_DbgPixelTarget.xy);

    float2 currentPos             = i.pos.xy;
    float2 nearestSegCoordEncoded = _MainTex.Load(int3(currentPos, 0)).rg;

    if (nearestSegCoordEncoded.y == FLOOD_NULL_POS)
        return half4(0, 0, 0, 0);

    // float2 scale           = contourScale(_TargetSize, _Contour_TexelSize);
    float2 nearestSegCoord = worldToContour(nearestSegCoordEncoded);

    float2 nearestPos  = nearestSegCoordEncoded;
    float2 nearestCell = floor(nearestSegCoord);
    float4 nearestSeg  = _Contour.Load(int3(nearestCell, 0));

    DBG_CODE(if (_dbgCenter) dbgDrawContourWindow(nearestCell);)

    float2 nearestA = contourToWorld(nearestCell, nearestSeg.xy);
    float2 nearestB = contourToWorld(nearestCell, nearestSeg.zw);
    nearestPos      = closestPointOnSegment(currentPos, nearestA, nearestB);

    DBG_LINE(currentPos, closestPointOnSegment(currentPos, nearestA, nearestB), DBG_COL_OLIVE);

    float arcLenToA = distance(nearestPos, nearestA);
    float arcLenToB = distance(nearestPos, nearestB);

    float  segLen  = distance(nearestA, nearestB);
    float  endEps  = segLen * 0.01;
    bool   atEndA  = arcLenToA < endEps;
    bool   atEndB  = arcLenToB < endEps;
    float2 toPixel = currentPos - nearestPos;

    if (atEndA || atEndB)
    {
        float2 endLocal = atEndA ? nearestSeg.xy : nearestSeg.zw;
        int2   nDir     = getNeighborDir(endLocal);
        float2 adjCell  = nearestCell + float2(nDir);
        float4 adjSeg   = _Contour.Load(int3(adjCell, 0));

        float2 adjA   = contourToWorld(adjCell, adjSeg.xy);
        float2 adjB   = contourToWorld(adjCell, adjSeg.zw);
        float2 adjDir = normalize(adjB - adjA);
        float2 curDir = normalize(nearestB - nearestA);

        bool adjBetter = abs(cross2(adjDir, toPixel)) > abs(cross2(curDir, toPixel));

        if (adjBetter)
        {
            nearestCell = adjCell;
            nearestSeg  = adjSeg;

            nearestA  = adjA;
            nearestB  = adjB;
            arcLenToA = distance(nearestPos, nearestA);
            arcLenToB = distance(nearestPos, nearestB);
        }
    }

    float d     = distance(currentPos, nearestPos);
    float sigma = min(_SigmaNear + _SigmaGrowth * sqrt(d), _SigmaMax);

    float walkRadius = 3 * sigma;

    int2  nearestADir = getNeighborDir(nearestSeg.xy);
    float turnA       = _Corner.Load(int3(int2(nearestCell) + nearestADir, 0)).b;
    float turnB       = _Corner.Load(int3(int2(nearestCell), 0)).b;
    float centerSegT  = arcLenToA / (arcLenToA + arcLenToB);
    float centerTurn  = lerp(turnA, turnB, centerSegT);

    float2 initDir = normalize(nearestB - nearestA);
    Chain  chainA  = chainInit(nearestSeg.xy, nearestCell, nearestA, initDir, arcLenToA, false);
    Chain  chainB  = chainInit(nearestSeg.zw, nearestCell, nearestB, initDir, arcLenToB, true);

    DBG_LINE(nearestA, nearestB, DBG_COL_OLIVE);
    DBG_LINE(nearestPos, nearestA, DbgHeat(getVertWeight(arcLenToA, sigma)));
    DBG_LINE(nearestPos, nearestB, DbgHeat(getVertWeight(arcLenToB, sigma)));

    float    wA        = getVertWeight(arcLenToA, sigma) * arcLenToA;
    float    wB        = getVertWeight(arcLenToB, sigma) * arcLenToB;
    CurveFit curveFitA = (CurveFit)0;
    CurveFit curveFitB = (CurveFit)0;
    curveFitAppend(curveFitA, nearestA, -arcLenToA, wA);
    curveFitAppend(curveFitB, nearestB, arcLenToB, wB);


    int s = 0;
    UNITY_LOOP
    for (s = 0; s < _ChainMaxSteps; s++)
    {
        float2 prevA = chainA.endPos;
        if (chainTryAdvance(chainA, walkRadius, centerTurn))
        {
            curveFitAppend(
                curveFitA, chainA.endPos, -chainA.cumArcLen,
                getVertWeight(chainA.cumArcLen, sigma) * chainA.segMass
            );
            DBG_LINE(prevA, chainA.endPos, DbgHeat(getVertWeight(chainA.cumArcLen, sigma)));
        }

        float2 prevB = chainB.endPos;
        if (chainTryAdvance(chainB, walkRadius, centerTurn))
        {
            curveFitAppend(
                curveFitB, chainB.endPos, chainB.cumArcLen,
                getVertWeight(chainB.cumArcLen, sigma) * chainB.segMass
            );
            DBG_LINE(prevB, chainB.endPos, DbgHeat(getVertWeight(chainB.cumArcLen, sigma)));
        }

        if (chainA.stopped && chainB.stopped) break;
    }

    curveFitNormalizeSide(curveFitA, chainA.endPos, -chainA.cumArcLen);
    curveFitNormalizeSide(curveFitB, chainB.endPos, chainB.cumArcLen);
    CurveFit curveFit = curveFitMerge(curveFitA, curveFitB);

    Curve  curve   = curveSolve(curveFit);
    float  ct      = curveClosestT(currentPos, curve);
    float2 closest = curveEval(curve, ct);
    float2 tangent = curveDeriv(curve, ct);
    float2 toFit   = closest - currentPos;
    float  fitDist = length(toFit);

    DBG_CODE(if (_dbgCenter) DBG_QUADRATIC(curve, DbgFade(DBG_COL_PURPLE, .75));)
    DBG_LINE(currentPos, closest, DBG_COL_PURPLE);

    float2 normal  = float2(-tangent.y, tangent.x);
    float  winding = dot(normal, -toFit);
    float  ssign   = winding >= 0 ? 1 : -1;

    float dist = fitDist * -ssign;

    float2 grad = fitDist > 1e-4 ? toFit / fitDist * ssign : -normalize(normal);

    DBG_ARROW(currentPos, currentPos + grad * 1, DBG_COL_BLUE);
    DBG_VALUE(9, encodeGrad(grad));

    half4 result = 0;
    result.ba    = encodeDist(dist);
    result.rg    = encodeGrad(grad);
    return result;
}

#endif
