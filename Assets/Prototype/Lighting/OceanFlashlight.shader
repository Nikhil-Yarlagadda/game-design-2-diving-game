Shader "Diving Prototype/Ocean Flashlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AmbientBrightness ("Ambient Brightness", Range(0,1)) = 0
        _ConeAngle ("Cone Angle", Range(2,180)) = 60
        _HalfStrengthDistance ("Half Strength Distance", Float) = 10
        _FlashlightOrigin ("Origin", Vector) = (0,0,0,0)
        _FlashlightDirection ("Direction", Vector) = (1,0,0,0)
        [HideInInspector] _SelfBlocker ("Self Blocker", Float) = -1
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
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FlashlightOrigin;
                float4 _FlashlightDirection;
                float _AmbientBrightness;
                float _ConeAngle;
                float _HalfStrengthDistance;
                float _SelfBlocker;
                float _EnableExternalAlpha;
                int _BlockerCount;
                float4 _BlockerRowsX[32];
                float4 _BlockerRowsY[32];
            CBUFFER_END

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

            bool Slab(float start, float step, inout float enter, inout float exit)
            {
                if (abs(step) < 0.000001) return abs(start) <= 1.0;
                float a = (-1.0 - start) / step;
                float b = (1.0 - start) / step;
                enter = max(enter, min(a, b));
                exit = min(exit, max(a, b));
                return enter <= exit;
            }

            bool Blocked(float2 endpoint, int index)
            {
                float4 rowX = _BlockerRowsX[index];
                if (rowX.w == 0.0 || index == (int)_SelfBlocker) return false;
                float4 rowY = _BlockerRowsY[index];
                float3 origin = float3(_FlashlightOrigin.xy, 1.0);
                float2 delta = endpoint - origin.xy;
                float2 start = float2(dot(rowX.xyz, origin), dot(rowY.xyz, origin));
                float2 step = float2(dot(rowX.xy, delta), dot(rowY.xy, delta));
                float enter = 0.0, exit = 1.0;
                return Slab(start.x, step.x, enter, exit)
                    && Slab(start.y, step.y, enter, exit)
                    && exit > 0.00001 && enter < 1.0;
            }

            half4 FlashlightFragment(Varyings input) : SV_Target
            {
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_EnableExternalAlpha > 0.0)
                    sampled.a = lerp(sampled.a, SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, input.uv).r, _EnableExternalAlpha);
                half4 color = sampled * input.color;
                float2 displacement = input.worldPosition - _FlashlightOrigin.xy;
                float distance = length(displacement);
                float halfAngle = radians(clamp(_ConeAngle, 2.0, 180.0) * 0.5);
                float outer = cos(halfAngle);
                float inner = cos(halfAngle - radians(1.0));
                float cone = distance < 0.001 ? 1.0 : smoothstep(outer, inner,
                    dot(displacement / distance, _FlashlightDirection.xy));
                float light = cone * exp2(-distance / max(0.001, _HalfStrengthDistance));
                if (light > 0.0)
                {
                    [loop] for (int i = 0; i < min(_BlockerCount, 32); i++)
                        if (Blocked(input.worldPosition, i)) { light = 0.0; break; }
                }
                color.rgb *= saturate(_AmbientBrightness) + (1.0 - saturate(_AmbientBrightness)) * light;
                return color;
            }
            ENDHLSL
        }
    }
}
