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

        [Header(Normal Map Detail)]
        [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _NormalTiling ("Normal Tiling", Range(0.5, 20)) = 4
        _NormalScrollSpeed ("Normal Scroll Speed", Range(0, 3)) = 0.3
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

            // Приём теней от основного источника света
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            // Точечные/спот-источники на сцене теперь тоже влияют на жидкость,
            // а не только один directional light
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            // Текстуры и сэмплеры всегда объявляются ВНЕ CBUFFER — иначе ломается SRP Batcher
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

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
                float _NormalStrength;
                float _NormalTiling;
                float _NormalScrollSpeed;
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
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                return output;
            }

            // Упрощённая GGX-функция распределения нормалей — даёт более естественную,
            // физически правдоподобную форму блика, чем грубый pow() из Blinn-Phong.
            // Имя с префиксом ChannelFill_, чтобы не конфликтовать со встроенной D_GGX из URP.
            float ChannelFill_D_GGX(float NdotH, float roughness)
            {
                float a = roughness * roughness;
                float a2 = a * a;
                float d = (NdotH * a2 - NdotH) * NdotH + 1.0;
                return a2 / (PI * d * d + 1e-7);
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

                // --- Normal map: два скроллящих слоя разной скорости/масштаба, ---
                // --- чтобы убрать видимую тайловость, как у классической воды ---
                float2 flowOffset1 = float2(_Time.y * _NormalScrollSpeed, 0.0);
                float2 flowOffset2 = float2(_Time.y * _NormalScrollSpeed * -0.6, 0.5);
                float2 normalUV1 = input.uv * _NormalTiling + flowOffset1;
                float2 normalUV2 = input.uv * _NormalTiling * 1.3 + flowOffset2;

                half3 normalTS1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV1), _NormalStrength);
                half3 normalTS2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV2), _NormalStrength);
                half3 normalTS = normalize(normalTS1 + normalTS2);

                // Вне залитой части канала (fillMask ~ 0) поверхность должна остаться
                // плоской — рябь только там, где реально есть жидкость.
                normalTS = normalize(lerp(half3(0, 0, 1), normalTS, fillMask));

                float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, normalWS);
                float3 mapNormalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));

                // Процедурная волна остаётся как более крупное, медленное движение поверх
                // детальной ряби из текстуры — веса снижены, чтобы не задваивать эффект.
                float3 bumpedNormalWS = normalize(
                    mapNormalWS + tangentWS * wave1 * 0.3 + bitangentWS * wave2 * 0.3);

                // Основной свет — теперь с учётом теней от других объектов на сцене
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDirectionWS = mainLight.direction;
                float ndotl = saturate(dot(bumpedNormalWS, lightDirectionWS));
                half3 ambient = SampleSH(normalWS);
                half3 mainLightContribution = mainLight.color * mainLight.shadowAttenuation * ndotl;
                half3 diffuse = baseColor * (mainLightContribution + ambient);

                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float roughness = max(1.0 - _Smoothness, 0.02);

                float3 halfDirectionWS = SafeNormalize(lightDirectionWS + viewDirectionWS);
                float ndoth = saturate(dot(bumpedNormalWS, halfDirectionWS));
                float specularMask = ChannelFill_D_GGX(ndoth, roughness) * ndotl * fillMask;
                half3 specular = _SpecularColor.rgb * specularMask * mainLight.color * mainLight.shadowAttenuation;

                // Точечные/спот-источники на сцене — раньше жидкость их полностью игнорировала
                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightsCount; lightIndex++)
                    {
                        Light extraLight = GetAdditionalLight(lightIndex, input.positionWS);
                        float extraNdotl = saturate(dot(bumpedNormalWS, extraLight.direction));
                        half3 extraAtten = extraLight.color
                            * (extraLight.distanceAttenuation * extraLight.shadowAttenuation);

                        diffuse += baseColor * extraAtten * extraNdotl;

                        float3 extraHalfDir = SafeNormalize(extraLight.direction + viewDirectionWS);
                        float extraNdoth = saturate(dot(bumpedNormalWS, extraHalfDir));
                        float extraSpecMask = ChannelFill_D_GGX(extraNdoth, roughness) * extraNdotl * fillMask;
                        specular += _SpecularColor.rgb * extraSpecMask * extraAtten;
                    }
                #endif

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
