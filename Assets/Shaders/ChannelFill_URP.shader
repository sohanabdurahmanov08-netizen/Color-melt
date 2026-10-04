Shader "ColorMelt/ChannelFillURP"
{
    // Paint flowing along a channel strip (the Liquid mesh of Color_route).
    // UV.x: 0 at the inlet, 1 at the outlet. UV.y: 0.25..0.75 across the strip.
    //
    // Mobile budget: a single variant (no shadows, no extra lights), lighting
    // terms per vertex, two fetches of one 256px texture, and the dry part of
    // the channel returns before any of the liquid maths.
    //
    // ChannelView drives _FillAmount, _FillColor and three timestamps in
    // Time.time, which URP passes to shaders as _Time.y:
    //   _FlowTime   last frame the paint moved; foam and waves calm down after it
    //   _MixTime    a new colour started to run in from the inlet over _PrevColor
    //   _SloshTime  the channel was picked up; the surface sloshes and settles
    Properties
    {
        _EmptyColor ("Empty Channel Color", Color) = (1, 1, 1, 1)
        _FillColor ("Fill Color", Color) = (1, 0, 0, 1)
        _FillAmount ("Fill Amount", Range(0, 1)) = 0
        [HideInInspector] _PrevColor ("Previous Color", Color) = (1, 0, 0, 1)
        [HideInInspector] _FlowTime ("Flow Time", Float) = -1000
        [HideInInspector] _MixTime ("Mix Time", Float) = -1000
        [HideInInspector] _SloshTime ("Slosh Time", Float) = -1000

        [Header(Shape)]
        _ChannelLength ("Channel Length (in widths)", Float) = 23.4
        _FrontBulge ("Front Bulge While Flowing", Range(0, 1)) = 0.4
        _SteepShade ("Steep Segment Shade", Range(0.5, 1)) = 0.9
        _WallShade ("Wall Contact Shade", Range(0, 1)) = 0.25
        _ContactShadow ("Shadow Ahead Of The Front", Range(0, 1)) = 0.3

        [Header(Surface)]
        [NoScaleOffset] _SurfaceTex ("Surface (RG ripple, B bubbles, A noise)", 2D) = "black" {}
        _RippleScale ("Ripple Scale", Range(0.05, 2)) = 0.3
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.1
        _FlowSpeed ("Flow Speed (widths/s)", Range(0, 4)) = 0.7
        _Streaks ("Colour Streaks", Range(0, 0.5)) = 0.06

        [Header(Waves)]
        _WaveHeight ("Wave Slope", Range(0, 0.6)) = 0.16
        _WaveLength ("Wave Length (widths)", Range(0.5, 8)) = 3.2
        _WaveSpeed ("Wave Speed (widths/s)", Range(0, 6)) = 1.5
        _FlowWaves ("Extra Waves While Flowing", Range(0, 4)) = 1
        _Slosh ("Slosh On Pick Up", Range(0, 1)) = 0.35

        [Header(Light)]
        _KeyLight ("Shading Light (tangent space)", Vector) = (-0.5, 0.3, 0.8, 0)
        _WaveShade ("Wave Shading", Range(0, 2)) = 1
        _SpecLight ("Highlight Light (tangent space)", Vector) = (0, 0.55, 0.85, 0)
        _Gloss ("Gloss", Range(8, 512)) = 120
        _SpecularIntensity ("Highlight Intensity", Range(0, 2)) = 0.5
        _Meniscus ("Meniscus", Range(0, 1)) = 0.45
        _FresnelIntensity ("Fresnel", Range(0, 1)) = 0.25
        _ReflectionColor ("Reflection Color", Color) = (1, 1, 1, 1)

        [Header(Foam)]
        _FoamLighten ("Foam Lighten", Range(0, 1)) = 0.5
        _FoamScale ("Bubble Scale", Range(0.1, 2)) = 0.25
        _FoamWidth ("Front Foam While Flowing (widths)", Range(0.01, 3)) = 1.2
        _FoamRestWidth ("Front Foam At Rest (widths)", Range(0.01, 1)) = 0.35
        _WallFoam ("Wall Foam", Range(0, 1)) = 0.2
        _InletFoam ("Inlet Foam", Range(0, 1)) = 0.5
        _CalmTime ("Calm Down Time (s)", Range(0.1, 5)) = 1.6

        [Header(Mixing)]
        _MixSpeed ("New Colour Speed (widths/s)", Range(1, 60)) = 50
        _Marble ("Marbling", Range(0, 3)) = 1.2
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
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SurfaceTex);
            SAMPLER(sampler_SurfaceTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _EmptyColor;
                half4 _FillColor;
                half4 _PrevColor;
                half4 _ReflectionColor;
                float4 _KeyLight;
                float4 _SpecLight;
                float _FillAmount;
                float _FlowTime;
                float _MixTime;
                float _SloshTime;
                float _ChannelLength;
                half _FrontBulge;
                half _SteepShade;
                half _WallShade;
                half _ContactShadow;
                float _RippleScale;
                half _RippleStrength;
                float _FlowSpeed;
                half _Streaks;
                half _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                half _FlowWaves;
                half _Slosh;
                half _WaveShade;
                half _Gloss;
                half _SpecularIntensity;
                half _Meniscus;
                half _FresnelIntensity;
                half _FoamLighten;
                float _FoamScale;
                half _FoamWidth;
                half _FoamRestWidth;
                half _WallFoam;
                half _InletFoam;
                half _CalmTime;
                float _MixSpeed;
                half _Marble;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                // Camera vector in tangent space, not normalised: it is linear
                // across a flat segment, so interpolating it stays exact.
                float3 viewTS : TEXCOORD1;
                half3 light : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs frame = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.uv = input.uv;

                float3 toCamera = GetCameraPositionWS() - position.positionWS;
                output.viewTS = float3(dot(toCamera, frame.tangentWS),
                    dot(toCamera, frame.bitangentWS), dot(toCamera, frame.normalWS));

                // Every segment is lit like the flat floor, so a colour reads the
                // same on the steep chute (Blue must not look Navy there); steep
                // parts only get a little darker to show the shape.
                Light mainLight = GetMainLight();
                half3 up = half3(0, 1, 0);
                half3 floorLight = mainLight.color * saturate(mainLight.direction.y) + SampleSH(up);
                half facing = saturate(dot(frame.normalWS, mainLight.direction));
                output.light = floorLight * lerp(_SteepShade, 1.0h, facing);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float channelLength = _ChannelLength;

                // Channel coordinates in channel widths: x along the flow, y across.
                float2 q = float2(input.uv.x * channelLength, input.uv.y * 2.0);
                half side = (half)(input.uv.y * 4.0 - 2.0);   // -1 .. 1, wall to wall
                half side2 = side * side;

                // 1 while the paint moves, then calms down to 0.
                half stir = (half)saturate(exp2((_FlowTime - time) * (3.0 / _CalmTime)));

                // A full channel also covers the outlet cap past UV 1.
                float fill = _FillAmount + step(0.999, _FillAmount) * 0.05;
                // The walls hold flowing paint back, so a moving front is rounded
                // and wobbles; at rest it lies flat against the block.
                float front = fill * channelLength - _FrontBulge * stir * side2
                    + 0.06 * stir * sin(side * 5.0 + time * 9.0);
                float depth = front - q.x;   // widths behind the front, < 0 is dry

                // Texture coordinates and their gradients are taken before the
                // branch so the samples inside it keep proper mip selection.
                float2 flow = float2(time * _FlowSpeed, 0.0);
                float2 scaleA = _RippleScale * float2(0.5, 1.0);   // streaks along the flow
                float scaleB = _FoamScale;
                float2 uvA = (q - flow) * scaleA;
                float2 uvB = (q - flow * 1.6) * scaleB + 0.37;
                float2 dqx = ddx(q);
                float2 dqy = ddy(q);

                half3 emptyColor = saturate(_EmptyColor.rgb * input.light);
                // Soft shadow on the dry floor right ahead of the front.
                half aheadShadow = saturate(1.0h + (half)depth * 5.0h) * _ContactShadow
                    * step(0.001, _FillAmount);
                emptyColor *= 1.0h - aheadShadow;

                UNITY_BRANCH
                if (depth < -0.25)
                    return half4(emptyColor, 1.0h);

                half4 texA = SAMPLE_TEXTURE2D_GRAD(_SurfaceTex, sampler_SurfaceTex, uvA, dqx * scaleA, dqy * scaleA);
                half4 texB = SAMPLE_TEXTURE2D_GRAD(_SurfaceTex, sampler_SurfaceTex, uvB, dqx * scaleB, dqy * scaleB);

                // ---- Surface normal (tangent space: x along the flow, y across) ----
                half2 tilt = (texA.rg + texB.rg - 1.0h) * _RippleStrength;

                // Light waves running downstream. The centre flows faster than
                // the walls, so the crests bow forward; they grow while flowing.
                float k = 6.2831853 / _WaveLength;
                float phase = k * (q.x - time * _WaveSpeed) + side2 * 1.2;
                float sin1, cos1, sin2, cos2;
                sincos(phase, sin1, cos1);
                sincos(phase * 1.7 + time * 0.7 + side * 0.6 + 1.3, sin2, cos2);
                half amplitude = _WaveHeight * (1.0h + _FlowWaves * stir);
                half height = amplitude * (half)(sin1 + 0.35 * sin2);
                tilt -= amplitude * half2(cos1 + 0.6 * cos2, side * 1.2h * (half)cos1 + 0.2 * cos2);

                // Picking the channel up rocks the paint from wall to wall.
                float since = max(time - _SloshTime, 0.0);
                half slosh = _Slosh * (half)(exp2(-since * 2.5) * sin(since * 13.0));
                half across = 1.0h - side2;   // ~cos(side * pi / 2)
                tilt.y -= slosh * across * (half)(0.75 + 0.25 * sin(q.x * 0.8 - since * 7.0));
                height += slosh * side * 0.5h;

                // Meniscus: the paint climbs the walls, so the normal leans inwards.
                half wall = saturate((abs(side) - 0.72h) * 3.57h);
                wall *= wall;
                tilt.y -= sign(side) * wall * _Meniscus;

                // Rounded lip of the front: the surface dives down to the floor.
                half lip = saturate(1.0h - (half)depth * 6.0h);
                tilt.x += lip * 0.9h;

                half3 normalTS = normalize(half3(tilt, 1.0h));
                half3 viewTS = (half3)normalize(input.viewTS);

                // ---- Paint colour: the new colour marbles in from the inlet ----
                float mixFront = (time - _MixTime) * _MixSpeed;
                half marble = ((texA.a - 0.5h) * 2.0h + (half)sin(q.x * 1.1 + side * 3.0) * 0.5h) * _Marble;
                half newPaint = saturate((half)(mixFront - q.x) * 1.5h + marble);
                half3 paint = lerp(_PrevColor.rgb, _FillColor.rgb, newPaint);
                // Lit and clamped first, as the old shader did, so each colour
                // looks the way players know it; waves, foam and highlights then
                // shade on top and stay visible on bright paint (yellow, white).
                paint = saturate(paint * input.light);
                paint *= 1.0h + (texA.a - 0.5h) * _Streaks + height * 0.3h;
                paint *= 1.0h - _WallShade * saturate((abs(side) - 0.88h) * 8.3h);

                // ---- Foam: a bubbly band behind the front, at the inlet and walls ----
                half frontFoam = saturate(1.0h - (half)depth / lerp(_FoamRestWidth, _FoamWidth, stir));
                half inletFoam = saturate(1.0h - (half)q.x * 0.6h) * _InletFoam * (0.5h + 0.5h * stir);
                half wallFoam = wall * _WallFoam * (0.5h + 0.5h * stir);
                half foamMask = max(frontFoam, max(inletFoam, wallFoam));
                // Low bubbles drop out first as the mask shrinks: the foam pops.
                half foam = saturate((texB.b - 1.0h + foamMask * 1.25h) * 4.0h);
                half3 foamColor = lerp(paint, half3(1, 1, 1), _FoamLighten) * (0.82h + 0.18h * texB.b);

                // ---- Light ----
                half3 keyLight = (half3)normalize(_KeyLight.xyz);
                half shade = 1.0h + _WaveShade * (dot(normalTS, keyLight) - keyLight.z);

                half3 color = lerp(paint, foamColor, foam) * shade;
                half fresnel = 1.0h - saturate(dot(normalTS, viewTS));
                fresnel *= fresnel;
                color = lerp(color, _ReflectionColor.rgb, fresnel * fresnel * _FresnelIntensity * (1.0h - foam));

                half3 halfDir = normalize((half3)normalize(_SpecLight.xyz) + viewTS);
                half specular = pow(saturate(dot(normalTS, halfDir)), _Gloss) * _SpecularIntensity;
                color += specular * (1.0h - foam);

                // Anti-aliased edge of the front over the dry floor.
                half wet = saturate((half)depth * 30.0h + 0.5h);
                return half4(lerp(emptyColor, color, wet), 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
