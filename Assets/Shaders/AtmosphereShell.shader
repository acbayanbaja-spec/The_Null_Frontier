// Additive fresnel shell drawn just outside the planet. The sprite atmosphere behind
// the planet supplies the bloom; this shell supplies the thin blue rim that hugs the
// silhouette and makes the body read as a world with air around it.
Shader "NullFrontier/AtmosphereShell"
{
    Properties
    {
        _Color ("Atmosphere", Color) = (0.30, 0.55, 1.0, 1)
        _Strength ("Strength", Float) = 0.9
        _Power ("Falloff", Range(1, 8)) = 3.2
        _SunDirection ("Sun Direction", Vector) = (-0.45, 0.35, 0.82, 0)
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
            Name "AtmosphereForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _SunDirection;
                float _Strength;
                float _Power;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float3 sunDir = normalize(_SunDirection.xyz);

                float rim = pow(1.0 - saturate(dot(normalWS, viewDir)), _Power);
                float lit = saturate(0.35 + saturate(dot(normalWS, sunDir)) * 0.9);
                float3 colour = _Color.rgb * rim * _Strength * lit;
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
