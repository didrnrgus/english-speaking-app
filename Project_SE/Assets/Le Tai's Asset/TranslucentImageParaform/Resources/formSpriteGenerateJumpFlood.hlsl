#ifndef FORM_SPRITE_GENERATE_JUMP_FLOOD_INCLUDED
#define FORM_SPRITE_GENERATE_JUMP_FLOOD_INCLUDED

#include "formSpriteGenerateCommon.hlsl"

float2 jumpFloodSeedFrag(v2f i) : SV_Target
{
    float2 coord = float2(i.pos.xy);
    float2 lo    = floor(worldToContour(coord - 0.5)) - 1;
    float2 hi    = floor(worldToContour(coord + 0.5));

    float  minDist = 1e9;
    float2 closest = FLOOD_NULL_POS_FLOAT2;

    UNITY_LOOP
    for (float y = lo.y; y <= hi.y; y++)
        for (float x = lo.x; x <= hi.x; x++)
        {
            float2 sampleCell = float2(x, y);
            sampleCell        = clamp(sampleCell, 0, _Contour_TexelSize.zw - 1);

            float4 lineData = _Contour.Load(int3(sampleCell, 0));

            float2 worldP0 = contourToWorld(sampleCell, lineData.xy);
            float2 worldP1 = contourToWorld(sampleCell, lineData.zw);

            float2 projection = closestPointOnSegment(coord, worldP0, worldP1);
            float  dist       = all(lineData == 0) ? 1e9 : distance(coord, projection);

            if (dist < minDist)
            {
                minDist = dist;
                closest = projection;
            }
        }

    return closest;
}

int _StepWidth;

float2 jumpFloodFrag(v2f i) : SV_Target
{
    int2 uvInt = int2(i.pos.xy);

    float  bestDist  = 1e30;
    float2 bestCoord = FLOOD_NULL_POS_FLOAT2;

    UNITY_UNROLL
    for (int u = -1; u <= 1; u++)
    {
        UNITY_UNROLL
        for (int v = -1; v <= 1; v++)
        {
            int2 offsetUV    = uvInt + int2(u, v) * _StepWidth;
            offsetUV         = clamp(offsetUV, int2(0, 0), (int2)_MainTex_TexelSize.zw - 1);
            float2 offsetPos = _MainTex.Load(int3(offsetUV, 0)).rg;
            float2 disp      = i.pos.xy - offsetPos;
            float  dist      = dot(disp, disp);

            if (offsetPos.y != FLOOD_NULL_POS && dist < bestDist)
            {
                bestDist  = dist;
                bestCoord = offsetPos;
            }
        }
    }

    return bestCoord;
}

#endif
