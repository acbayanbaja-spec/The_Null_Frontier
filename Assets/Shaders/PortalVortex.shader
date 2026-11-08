// Energy membrane inside the gate. Additive, so the planet stays visible through it;
// the swirl and the rim are driven by shader time, which keeps the menu's runtime cost
// to one draw call and no per-frame CPU work.
Shader "NullFrontier/PortalVortex"
{
    Properties
    {
        _Color ("Energy Colour", Color) = (0.35, 0.78, 1.0, 1)
        _Intensity ("Intensity", Float) = 1.1
        _Speed ("Speed", Float) = 0.5
        _Ribbons ("Ribbons", Range(1, 12)) = 5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VortexForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Speed;
                float _Ribbons;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                if (radius > 1.0) discard;

                float angle = atan2(p.y, p.x);
                float t = _Time.y * _Speed;

                // Two counter-rotating spirals: straight sine bands read as a pinwheel,
                // the wobble on the angle breaks the pattern up into filaments.
                float spiral = angle * _Ribbons + radius * 9.0 - t * 3.0
                             + sin(angle * 3.0 + t * 1.3) * 1.2;
                float ribbons = 0.5 + 0.5 * sin(spiral);
                ribbons = pow(ribbons, 7.0);

                // Inverted rising edges instead of smoothstep(1.0, 0.72, x): a reversed
                // smoothstep range is undefined on some mobile shader compilers.
                float inner = smoothstep(0.02, 0.28, radius)
                            * (1.0 - smoothstep(0.72, 1.0, radius));
                float rim = smoothstep(0.66, 0.92, radius)
                          * (1.0 - smoothstep(0.90, 1.0, radius));
                float veil = 0.06 * smoothstep(0.06, 0.5, radius);

                float energy = ribbons * inner * 0.55 + rim * 0.85 + veil;
                float3 colour = _Color.rgb * energy * _Intensity;

                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
