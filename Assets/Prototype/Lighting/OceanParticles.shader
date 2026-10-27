Shader "Diving Prototype/Ocean Particles"
{
    Properties
    {
        _PlayerHaloStrength ("Player Halo Strength", Range(0,1)) = 1
        _Color ("Tint", Color) = (1,1,1,1)
        _BubbleStyle ("Bubble Style", Float) = 1
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
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 worldPosition : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.worldPosition = world.xy;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float aa = max(fwidth(radius), 0.025);
                float ring = (1.0 - smoothstep(0.76, 0.76 + aa, radius)) * smoothstep(0.50, 0.65, radius);
                float highlight = exp(-dot(p - float2(-0.32, 0.38), p - float2(-0.32, 0.38)) * 45.0);
                float mote = pow(saturate(1.0 - radius), 2.0);
                float shape = _BubbleStyle > 0.5 ? saturate(ring * 0.65 + highlight) : mote;
                clip(shape - 0.002);
                float light = OceanIllumination(input.worldPosition);
                return half4(input.color.rgb, input.color.a * shape * light);
            }
            ENDHLSL
        }
    }
}
