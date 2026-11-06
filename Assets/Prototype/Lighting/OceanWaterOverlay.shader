Shader "Diving Prototype/Ocean Water Overlay"
{
    Properties
    {
        _PlayerHaloStrength ("Player Halo Strength", Range(0,1)) = 1
        _Color ("Tint", Color) = (0.26,0.65,0.75,1)
        _SelfBlocker ("Self Blocker", Float) = -1
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "OceanLighting.hlsl"
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 worldPosition : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.worldPosition = world.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float light = OceanIllumination(input.worldPosition);
                if (light < .001) return half4(0,0,0,0);
                float time = _OceanTime * _WaterSpeed;
                float ripples = OceanRipples(input.worldPosition * .8, time);
                float broadWave = .5 + .5 * sin(dot(input.worldPosition, float2(.65,1.1)) + time * .5);
                float alpha = _WaterStrength * light * (.15 * pow(broadWave, 6.0) + .25 * ripples);
                return half4(_Color.rgb * (.7 + light * .5), alpha);
            }
            ENDHLSL
        }
    }
}
