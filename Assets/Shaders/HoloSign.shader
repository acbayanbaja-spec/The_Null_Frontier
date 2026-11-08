// Abstract holographic shopfront signage: blocky glyph columns scrolling behind a
// scanline, with a thin frame. Additive and unlit so it reads as emitted light on the
// dark facades without a texture or an extra light.
Shader "NullFrontier/HoloSign"
{
    Properties
    {
        _Color ("Sign Colour", Color) = (0.2, 0.9, 1.0, 1)
        _Intensity ("Intensity", Float) = 1.0
        _Scroll ("Scroll Speed", Float) = 1.2
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
            Name "HoloForward"
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
                float _Scroll;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 scrolled = float2(input.uv.x, input.uv.y + _Time.y * _Scroll * 0.06);
                float2 grid = float2(scrolled.x * 9.0, scrolled.y * 26.0);
                float2 cell = floor(grid);
                float2 local = frac(grid);

                float hash = Hash21(cell);
                float glyph = step(0.46, hash);
                // Rising edge times an inverted rising edge rather than a smoothstep with
                // reversed edges, which is undefined on some mobile compilers.
                float column = smoothstep(0.12, 0.28, local.x)
                             * (1.0 - smoothstep(0.72, 0.88, local.x));
                float row = smoothstep(0.18, 0.34, local.y)
                          * (1.0 - smoothstep(0.66, 0.82, local.y));
                float blocks = glyph * column * row;

                float scanline = 0.75 + 0.25 * sin(scrolled.y * 420.0 - _Time.y * 9.0);
                float flicker = 0.88 + 0.12 * sin(_Time.y * 11.0 + hash * 13.0);

                float2 edge = min(input.uv, 1.0 - input.uv);
                float frame = 1.0 - smoothstep(0.0, 0.012, min(edge.x, edge.y));

                float3 colour = _Color.rgb * (blocks * 0.55 + frame * 0.45)
                              * scanline * flicker * _Intensity;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
