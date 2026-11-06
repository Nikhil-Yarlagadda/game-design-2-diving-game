#ifndef OCEAN_LIGHTING_INCLUDED
#define OCEAN_LIGHTING_INCLUDED
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    float4 _FlashlightOrigin, _FlashlightDirection;
    float _AmbientBrightness, _ConeAngle, _CoreAngle, _SpillAngle, _SpillInnerAngle;
    float _HalfStrengthDistance, _BeamFadeStart, _BeamRange, _BeamIntensity;
    float _SpillIntensity, _SpillFadeStart, _SpillRange;
    float _PlayerLightRadius, _HaloFeather, _PlayerHaloStrength;
    float _SelfBlocker, _Reveal, _EnableExternalAlpha;
    float _SceneryEffects, _ObstacleEffects;
    float4 _SpriteUVRect, _WorldToSpriteU, _WorldToSpriteV;
    float _WaterStrength, _RefractionTexels, _WaterSpeed, _OceanTime, _BubbleStyle;
    int _BlockerCount;
    float4 _BlockerRowsX[32], _BlockerRowsY[32];
    int _EdgeCount;
    float4 _PolygonEdges[256];
    float _EdgeOwners[256];
CBUFFER_END

// Exponential shoulders have no visible outer circle or straight cone seam.
float OceanCircle(float distance)
{
    float shoulder = max(0.0, distance - _PlayerLightRadius) / max(0.001, _HaloFeather);
    return _PlayerLightRadius > 0.0 ? saturate(_PlayerHaloStrength) * exp2(-2.0 * pow(shoulder, 1.5))
        * (1.0 - smoothstep(3.0, 4.0, shoulder)) : 0.0;
}

float OceanAngularMask(float cosine, float innerAngle, float outerAngle)
{
    float angle = degrees(acos(clamp(cosine, -1.0, 1.0)));
    float outer = outerAngle * 0.5;
    float shoulder = angle / max(0.5, innerAngle * 0.5);
    return exp2(-shoulder * shoulder) * (1.0 - smoothstep(outer * 0.65, outer, angle));
}

float OceanForwardLight(float2 displacement)
{
    float distance = length(displacement);
    float cosine = distance > 0.00001 ? dot(displacement / distance, _FlashlightDirection.xy) : 1.0;
    float beam = _BeamIntensity * OceanAngularMask(cosine, _CoreAngle, _ConeAngle)
        * exp2(-distance / max(0.001, _HalfStrengthDistance))
        * (1.0 - smoothstep(_BeamFadeStart, max(_BeamFadeStart + 0.001, _BeamRange), distance));
    float spill = _SpillIntensity * OceanAngularMask(cosine, _SpillInnerAngle, _SpillAngle)
        * exp2(-distance / max(0.001, _HalfStrengthDistance * 2.0))
        * (1.0 - smoothstep(_SpillFadeStart, max(_SpillFadeStart + 0.001, _SpillRange), distance));
    return 1.0 - (1.0 - saturate(beam)) * (1.0 - saturate(spill));
}

// Shared by the water veil and surface shading: moving, intersecting refracted wave fronts.
float OceanRipples(float2 position, float time)
{
    float2 p = position * 1.15;
    p += float2(sin(p.y * .83 + sin(p.x * .47) - time * .18),
        cos(p.x * .67 + sin(p.y * .51) + time * .21)) * 1.05;
    float bend = sin(p.y * 0.7 - time * 0.63) * 0.65 + sin(p.x * 0.45 + time * 0.4) * 0.45;
    float a = sin(p.x * 2.4 + p.y * 1.1 + bend + time);
    float b = sin(p.x * -1.35 + p.y * 2.8 - bend - time * 0.78);
    float ribbons = pow(saturate(1.0 - abs(a + b) * 0.65), 5.0);
    return ribbons;
}

bool OceanSlab(float start, float step, inout float enter, inout float exit)
{
    if (abs(step) < 0.000001) return abs(start) <= 1.0;
    float a = (-1.0 - start) / step, b = (1.0 - start) / step;
    enter = max(enter, min(a, b));
    exit = min(exit, max(a, b));
    return enter <= exit;
}

bool OceanBlocked(float2 endpoint)
{
    float2 ray = endpoint - _FlashlightOrigin.xy;
    [loop] for (int i = 0; i < min(_BlockerCount, 32); i++)
    {
        float4 rowX = _BlockerRowsX[i], rowY = _BlockerRowsY[i];
        if (rowX.w == 0.0 || i == (int)_SelfBlocker) continue;
        float3 origin = float3(_FlashlightOrigin.xy, 1.0);
        float2 start = float2(dot(rowX.xyz, origin), dot(rowY.xyz, origin));
        float2 step = float2(dot(rowX.xy, ray), dot(rowY.xy, ray));
        float enter = 0.0, exit = 1.0;
        if (OceanSlab(start.x, step.x, enter, exit) && OceanSlab(start.y, step.y, enter, exit)
            && exit > 0.00001 && enter < 1.0) return true;
    }
    [loop] for (int e = 0; e < min(_EdgeCount, 256); e++)
    {
        if ((int)_EdgeOwners[e] == (int)_SelfBlocker) continue;
        float2 a = _PolygonEdges[e].xy;
        float2 edge = _PolygonEdges[e].zw - a;
        float2 offset = a - _FlashlightOrigin.xy;
        float divisor = ray.x * edge.y - ray.y * edge.x;
        if (abs(divisor) < 0.000001) continue;
        float t = (offset.x * edge.y - offset.y * edge.x) / divisor;
        float u = (offset.x * ray.y - offset.y * ray.x) / divisor;
        if (t > 0.00001 && t < 0.99999 && u >= 0.0 && u <= 1.0) return true;
    }
    return false;
}

float OceanIllumination(float2 worldPosition)
{
    float2 displacement = worldPosition - _FlashlightOrigin.xy;
    float circle = OceanCircle(length(displacement));
    float forward = OceanForwardLight(displacement);
    // No blocker work is needed when the always-lit halo already dominates.
    if (forward > circle && OceanBlocked(worldPosition)) forward = 0.0;
    return 1.0 - (1.0 - circle) * (1.0 - forward);
}
#endif
