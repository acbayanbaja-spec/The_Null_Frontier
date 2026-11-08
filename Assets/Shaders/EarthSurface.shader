// Lit Earth for the menu backdrop. The scene sun points away from the hemisphere the
// camera sees, so the planet carries its own fixed sun instead of a second directional
// light: a terminator, warm sunset band, ocean specular, cloud shadowing and night-side
// city lights are cheaper than another realtime light in a mobile forward pipeline.
Shader "NullFrontier/EarthSurface"
{
    Properties
    {
        _MainTex ("Day Albedo", 2D) = "white" {}
        _CloudTex ("Cloud Coverage", 2D) = "black" {}
        _SunDirection ("Sun Direction", Vector) = (-0.45, 0.35, 0.82, 0)
        _SunColor ("Sun Colour", Color) = (1.0, 0.96, 0.90, 1)
        _AmbientColor ("Ambient", Color) = (0.10, 0.14, 0.24, 1)
        _NightColor ("Night Tint", Color) = (0.015, 0.022, 0.045, 1)
        _AtmosphereColor ("Atmosphere", Color) = (0.28, 0.52, 1.0, 1)
        _AtmosphereStrength ("Atmosphere Strength", Float) = 0.55
        _SunsetColor ("Sunset", Color) = (1.0, 0.55, 0.22, 1)
        _SunsetStrength ("Sunset Strength", Float) = 0.6
        _SpecColor ("Ocean Specular", Color) = (0.75, 0.88, 1.0, 1)
        _SpecIntensity ("Ocean Specular Intensity", Float) = 1.2
        _Smoothness ("Ocean Smoothness", Range(0, 1)) = 0.86
        _CityLightColor ("Night Lights", Color) = (1.0, 0.70, 0.32, 1)
        _CityLightStrength ("Night Lights Strength", Range(0, 2)) = 0.75
        _CloudShadow ("Cloud Shadow", Range(0, 1)) = 0.35
        _CloudOffset ("Cloud Shadow Offset", Vector) = (0.013, 0.0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "EarthForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _SunDirection;
                float4 _SunColor;
                float4 _AmbientColor;
                float4 _NightColor;
                float4 _AtmosphereColor;
                float4 _SunsetColor;
                float4 _SpecColor;
                float4 _CityLightColor;
                float4 _CloudOffset;
                float _AtmosphereStrength;
                float _SunsetStrength;
                float _SpecIntensity;
                float _Smoothness;
                float _CityLightStrength;
                float _CloudShadow;
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

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 sunDir = normalize(_SunDirection.xyz);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));

                float ndl = dot(normalWS, sunDir);
                float ndv = saturate(dot(normalWS, viewDir));

                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                float luminance = dot(albedo, float3(0.2126, 0.7152, 0.0722));

                // The source map's water is blue and dark; ice and desert stay out of the
                // mask because they are bright or red-dominant.
                float ocean = saturate((albedo.b - albedo.r * 1.05) * 3.0)
                            * saturate(1.0 - luminance * 2.4);
                float land = saturate(1.0 - ocean * 2.0);

                float clouds = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex,
                    input.uv + _CloudOffset.xy).r;
                float cloudShade = 1.0 - clouds * _CloudShadow;

                float sun = saturate(ndl);
                float3 dayColour = albedo * (_AmbientColor.rgb + _SunColor.rgb * sun * 1.25)
                                 * cloudShade;

                float3 halfway = normalize(sunDir + viewDir);
                float specular = pow(saturate(dot(normalWS, halfway)),
                    lerp(24.0, 260.0, _Smoothness)) * ocean * _SpecIntensity
                    * smoothstep(0.0, 0.25, ndl);

                float night = 1.0 - smoothstep(-0.10, 0.22, ndl);
                float2 grid = input.uv * float2(240.0, 120.0);
                float2 cell = floor(grid);
                float2 local = frac(grid) - 0.5;
                float hash = Hash21(cell);
                float cluster = step(0.68, Hash21(floor(cell * 0.25)));
                float sparkle = step(0.80, hash) * cluster
                              * (1.0 - smoothstep(0.0, 0.38, length(local)));
                float twinkle = 0.65 + 0.35 * sin(_Time.y * 1.7 + hash * 40.0);
                float3 cityLights = _CityLightColor.rgb * sparkle * twinkle
                                  * land * (1.0 - clouds * 0.85) * _CityLightStrength;

                float3 nightColour = albedo * _NightColor.rgb + cityLights;
                float3 colour = lerp(nightColour, dayColour, smoothstep(-0.06, 0.18, ndl));
                colour += specular * _SpecColor.rgb;

                // The lit limb scatters blue; the terminator picks up a warm sunset band.
                float rim = pow(1.0 - ndv, 3.0);
                colour += _AtmosphereColor.rgb * rim * _AtmosphereStrength
                        * saturate(0.35 + sun * 0.85);

                float terminator = pow(saturate(1.0 - abs(ndl) * 2.2), 4.0);
                colour += _SunsetColor.rgb * terminator * _SunsetStrength
                        * saturate(ndl + 0.4) * rim;

                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
