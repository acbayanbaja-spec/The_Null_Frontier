// Cloud shell for the menu planet. Coverage comes from the grayscale cloud sheet;
// the colour is shaded by the same fixed sun as the surface so the weather darkens
// through the terminator instead of glowing like a decal.
Shader "NullFrontier/EarthClouds"
{
    Properties
    {
        _MainTex ("Cloud Coverage", 2D) = "black" {}
        _SunDirection ("Sun Direction", Vector) = (-0.45, 0.35, 0.82, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.90, 1)
        _NightColor ("Night Tint", Color) = (0.05, 0.07, 0.12, 1)
        _Tint ("Cloud Tint", Color) = (1.0, 1.0, 1.0, 1)
        _SunsetColor ("Sunset", Color) = (1.0, 0.55, 0.22, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _Softness ("Limb Softness", Range(0.01, 0.5)) = 0.16
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
            Name "CloudForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SunDirection;
                float4 _SunColor;
                float4 _NightColor;
                float4 _Tint;
                float4 _SunsetColor;
                float _Opacity;
                float _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                float coverage = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
                float ndl = dot(normalWS, sunDir);
                float ndv = saturate(dot(normalWS, viewDir));

                float alpha = saturate(coverage * _Opacity);
                // Fade the shell out at the silhouette, otherwise its geometry edge reads
                // as a hard ring around the planet.
                alpha *= smoothstep(0.0, _Softness, ndv);
                if (alpha <= 0.001) discard;

                float sun = saturate(ndl);
                float day = smoothstep(-0.10, 0.25, ndl);
                float3 lit = _Tint.rgb * _SunColor.rgb * (0.22 + 0.95 * sun);
                float3 night = _Tint.rgb * _NightColor.rgb;
                float3 colour = lerp(night, lit, day);

                float terminator = pow(saturate(1.0 - abs(ndl) * 2.2), 4.0);
                colour += _SunsetColor.rgb * terminator * 0.5;

                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
