Shader "ColorMelt/ChannelFillURP"
{
    // UV.x: 0 at the channel inlet, 1 at the outlet. UV.y runs across it.
    Properties
    {
        _EmptyColor ("Empty Channel Color", Color) = (0.85, 0.85, 0.85, 1)
        _FillColor ("Fill Color", Color) = (1, 0, 0, 1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.03
        _EdgeGlow ("Edge Glow Color", Color) = (1, 1, 1, 1)
        _EdgeGlowWidth ("Edge Glow Width", Range(0, 0.1)) = 0.02

        [Header(Liquid Surface)]
        _Smoothness ("Smoothness (Specular Sharpness)", Range(0,1)) = 0.85
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _FresnelColor ("Fresnel Rim Color", Color) = (1,1,1,1)
        _FresnelIntensity ("Fresnel Intensity", Range(0,2)) = 0.6

        [Header(Flow Animation)]
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 1.2
        _WaveFrequency ("Wave Frequency", Range(1, 60)) = 20
        _WaveStrength ("Wave Strength (Normal Distortion)", Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ChannelForward"
            // Render this pass in both URP Forward and Deferred renderers.
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Keep the original property names for existing material updates.
            CBUFFER_START(UnityPerMaterial)
                half4 _EmptyColor;
                half4 _FillColor;
                half4 _EdgeGlow;
                half4 _SpecularColor;
                half4 _FresnelColor;
                float _FillAmount;
                float _EdgeSoftness;
                float _EdgeGlowWidth;
                float _Smoothness;
                float _FresnelPower;
                float _FresnelIntensity;
                float _FlowSpeed;
                float _WaveFrequency;
                float _WaveStrength;
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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float progress = input.uv.x;
                float softness = max(_EdgeSoftness, 0.00001);
                // Same transition as the original, with ascending smoothstep edges.
                float fillMask = 1.0 - smoothstep(
                    _FillAmount - softness, _FillAmount, progress);
                half3 baseColor = lerp(_EmptyColor.rgb, _FillColor.rgb, fillMask);

                float edgeDistance = abs(progress - _FillAmount);
                float edgeWidth = max(_EdgeGlowWidth, 0.00001);
                float edgeMask = 1.0 - smoothstep(0.0, edgeWidth, edgeDistance);
                edgeMask *= step(progress, _FillAmount + _EdgeGlowWidth);
                edgeMask *= _EdgeGlowWidth > 0.0 ? 1.0 : 0.0;
                baseColor = lerp(baseColor, _EdgeGlow.rgb, edgeMask * _EdgeGlow.a);

                float wave1 = sin(progress * _WaveFrequency - _Time.y * _FlowSpeed)
                    * _WaveStrength;
                float wave2 = sin(input.uv.y * (_WaveFrequency * 0.6)
                    + _Time.y * _FlowSpeed * 0.7) * _WaveStrength * 0.5;

                float3 normalWS = SafeNormalize(input.normalWS);
                // Avoid a zero cross product when the surface faces the reference axis.
                float3 referenceAxis = abs(normalWS.z) < 0.999
                    ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 tangentWS = SafeNormalize(cross(normalWS, referenceAxis));
                float3 bitangentWS = cross(normalWS, tangentWS);
                float3 bumpedNormalWS = SafeNormalize(
                    normalWS + tangentWS * wave1 + bitangentWS * wave2);

                // Original lighting model: main directional light plus ambient/probes.
                Light mainLight = GetMainLight();
                float3 lightDirectionWS = mainLight.direction;
                float ndotl = saturate(dot(bumpedNormalWS, lightDirectionWS));
                half3 ambient = SampleSH(normalWS);
                half3 diffuse = baseColor * (mainLight.color * ndotl + ambient);

                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);
                float specularPower = lerp(8.0, 200.0, _Smoothness);
                float specularMask = pow(
                    saturate(dot(bumpedNormalWS, halfDirectionWS)), specularPower) * fillMask;
                half3 specular = _SpecularColor.rgb * specularMask * mainLight.color;

                float fresnel = pow(1.0 - saturate(dot(bumpedNormalWS, viewDirectionWS)),
                    _FresnelPower) * _FresnelIntensity * fillMask;
                half3 rim = _FresnelColor.rgb * fresnel;

                return half4(diffuse + specular + rim, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
