#ifndef FORM_SPRITE_INCLUDED
#define FORM_SPRITE_INCLUDED

float _DistanceRange;

float2 encodeDist(float distance, float distanceRange)
{
    float scaledDistance = saturate(distance / distanceRange * .5 + .5);
    return float2(
        floor(scaledDistance * 255.) / 255.,
        frac(scaledDistance * 255.)
    );
}

float decodeDist(float2 encoded, float distanceRange)
{
    float distance = encoded.x;
    distance += encoded.y / 255.;
    return (distance * 2 - 1) * distanceRange;
}

float2 encodeDist(float distance)
{
    return encodeDist(distance, _DistanceRange);
}

float decodeDist(float2 encoded)
{
    return decodeDist(encoded, _DistanceRange);
}

float2 encodeGrad(float2 gradient)
{
    return gradient * .5 + .5;
}

float2 decodeGrad(float2 encoded)
{
    return encoded * 2 - 1;
}

#endif
