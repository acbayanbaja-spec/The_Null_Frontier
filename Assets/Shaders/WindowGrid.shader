// Lit office windows painted additively onto a building face. One quad per facade,
// no extra geometry: the grid, the lit ratio and the window tint are evaluated per
// cell, seeded from the object's world position so every block looks different.
Shader "NullFrontier/WindowGrid"
{
    Properties
    {
        _CoolColor ("Cool Windows", Color) = (0.40, 0.72, 1.0, 1)
        _WarmColor ("Warm Windows", Color) = (1.0, 0.68, 0.34, 1)
        _Columns ("Columns", Float) = 4
        _Rows ("Rows", Float) = 11
        _LitRatio ("Lit Ratio", Range(0, 1)) = 0.42
        _Intensity ("Intensity", Float) = 1.0
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
            Name "WindowForward"
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
                float4 _CoolColor;
                float4 _WarmColor;
                float _Columns;
                float _Rows;
                float _LitRatio;
                float _Intensity;
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
                float2 grid = input.uv * float2(_Columns, _Rows);
                float2 cell = floor(grid);
                float2 local = frac(grid);

                // A pane is the area between 0.26 and 0.74 on both axes. Written as a
                // rising edge times an inverted rising edge: smoothstep with its edges
                // reversed is undefined on some mobile compilers.
                float2 pane = smoothstep(0.16, 0.26, local)
                            * (1.0 - smoothstep(0.74, 0.84, local));
                float mask = pane.x * pane.y;

                float seed = dot(unity_ObjectToWorld._m03_m13_m23,
                    float3(0.37, 0.91, 0.53));
                float hash = Hash21(cell + seed);
                float lit = step(1.0 - _LitRatio, hash);
                float warm = step(0.68, Hash21(cell * 1.7 + seed));

                float3 tint = lerp(_CoolColor.rgb, _WarmColor.rgb, warm);
                float flicker = 0.85 + 0.15 * sin(_Time.y * 1.6 + hash * 27.0);
                float3 colour = tint * lit * mask * (0.35 + 0.75 * hash)
                              * _Intensity * flicker;

                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
