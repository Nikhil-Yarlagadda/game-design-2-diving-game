Shader "Diving Prototype/Ocean Flashlight"
{
    Properties
    {
        _PlayerHaloStrength ("Player Halo Strength", Range(0,1)) = 1
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AmbientBrightness ("Ambient Brightness", Range(0,1)) = 0
        _ConeAngle ("Cone Angle", Range(2,180)) = 125
        _HalfStrengthDistance ("Half Strength Distance", Float) = 10
        _CoreAngle ("Bright Core Angle", Float) = 44
        _BeamIntensity ("Beam Intensity", Range(0,1)) = 1
        _SpillAngle ("Spill Outer Angle", Float) = 178
        _SpillInnerAngle ("Spill Inner Angle", Float) = 90
        _SpillIntensity ("Spill Intensity", Range(0,1)) = 0.18
        _BeamFadeStart ("Beam Fade Start", Float) = 24
        _BeamRange ("Beam Range", Float) = 36
        _SpillFadeStart ("Spill Fade Start", Float) = 22
        _SpillRange ("Spill Range", Float) = 34
        _HaloFeather ("Halo Feather", Float) = 2.5
        _WaterStrength ("Water Shimmer", Range(0,1)) = 0.55
        _RefractionTexels ("Refraction Texels", Range(0,4)) = 2
        _WaterSpeed ("Water Speed", Float) = 0.6
        [PerRendererData] _SceneryEffects ("Scenery Effects", Float) = 0
        [PerRendererData] _ObstacleEffects ("Obstacle Effects", Float) = 0
        [PerRendererData] _SpriteUVRect ("Sprite UV Bounds", Vector) = (0,0,1,1)
        _PlayerLightRadius ("Player Light Radius", Float) = 0.75
        _FlashlightOrigin ("Origin", Vector) = (0,0,0,0)
        _FlashlightDirection ("Direction", Vector) = (1,0,0,0)
        [HideInInspector] _SelfBlocker ("Self Blocker", Float) = -1
        [PerRendererData] _Reveal ("Highlight Reveal", Range(0,1)) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex FlashlightVertex
            #pragma fragment FlashlightFragment
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                float2 worldPosition : TEXCOORD1;
                half4 color : COLOR;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);
            float4 _MainTex_TexelSize;
            #include "OceanLighting.hlsl"

            Varyings FlashlightVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.worldPosition = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 FlashlightFragment(Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_EnableExternalAlpha > 0.0)
                    sampled.a = lerp(sampled.a, SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, input.uv).r, _EnableExternalAlpha);
                half4 color = sampled * input.color;
                if (color.a == 0.0 || _Reveal >= 1.0 || _AmbientBrightness >= 1.0) return color;
                float light = OceanIllumination(input.worldPosition);
                if (_SceneryEffects > 0.0 && light > 0.0)
                {
                    float2 p = input.worldPosition;
                    float time = _OceanTime * _WaterSpeed;
                    float caustic = OceanRipples(p, time);
                    float2 ripple = float2(sin(p.y * 2.1 + time), cos(p.x * 1.8 - time * 0.8));
                    float2 inset = min(_MainTex_TexelSize.xy * 0.5, (_SpriteUVRect.zw - _SpriteUVRect.xy) * 0.49);
                    float2 uv = clamp(input.uv + ripple * _MainTex_TexelSize.xy * _RefractionTexels * light,
                        _SpriteUVRect.xy + inset, _SpriteUVRect.zw - inset);
                    half4 refracted = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                    // Keep original silhouettes; reject displaced samples outside the sprite's painted area.
                    color.rgb = lerp(sampled.rgb, refracted.rgb, saturate(refracted.a)) * input.color.rgb;
                    // Refracted light creates visible moving highlights and darker troughs on flat art too.
                    color.rgb *= 1.0 + _WaterStrength * (caustic * 0.8 - 0.22);
                    if (_ObstacleEffects > 0.0)
                    {
                        float2 towardLight = normalize(_FlashlightOrigin.xy - p + float2(0.00001, 0));
                        float2 uvPerWorld = float2(dot(_WorldToSpriteU.xy, towardLight), dot(_WorldToSpriteV.xy, towardLight));
                        float2 nearUV = input.uv + uvPerWorld * 0.22;
                        float2 farUV = input.uv + uvPerWorld * 0.65;
                        float nearInside = all(nearUV >= _SpriteUVRect.xy) && all(nearUV <= _SpriteUVRect.zw) ? 1.0 : 0.0;
                        float farInside = all(farUV >= _SpriteUVRect.xy) && all(farUV <= _SpriteUVRect.zw) ? 1.0 : 0.0;
                        float nearAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, clamp(nearUV, _SpriteUVRect.xy, _SpriteUVRect.zw)).a * nearInside;
                        float farAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, clamp(farUV, _SpriteUVRect.xy, _SpriteUVRect.zw)).a * farInside;
                        float bevel = saturate(sampled.a - farAlpha);
                        float lip = saturate(sampled.a - nearAlpha);
                        // Broad surface planes react to the light direction, independent of the painted texture.
                        float2 slope = float2(sin(p.x * 1.1 + p.y * .6), cos(p.y * .9 - p.x * .5)) * .65;
                        float3 normal = normalize(float3(slope, 1.0));
                        float diffuse = saturate(dot(normal, normalize(float3(towardLight, .6))));
                        float surface = .3 + diffuse * .85;
                        float shadedCaustic = OceanRipples(p + slope * .22, time * 1.1);
                        color.rgb *= lerp(1.0, surface * (.72 + shadedCaustic * .65), _WaterStrength);
                        color.rgb += (float3(.18, .23, .25) * bevel + float3(.16, .2, .21) * lip)
                            * _WaterStrength * (.55 + shadedCaustic * .45);
                    }
                }
                color.rgb *= max(saturate(_Reveal), saturate(_AmbientBrightness) + (1.0 - saturate(_AmbientBrightness)) * light);
                return color;
            }
            ENDHLSL
        }
    }
}
