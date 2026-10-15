Shader "ColorMelt/PaintFall"
{
    // Paint pouring off the end of a channel (OutletFall's ribbon mesh, which
    // always faces the camera).
    // UV.x: flight time of that point in seconds since its paint left the
    //       outlet. _Time.y - UV.x is when that paint left, so every wobble,
    //       streak and bead below rides with its own bit of paint, and
    //       stretches as the paint speeds up, without any scrolling.
    // UV.y: 0..1 across the ribbon.
    // The mesh already narrows the stream as it accelerates (continuity);
    // here it wobbles, necks and finally breaks up into drops the longer it
    // falls (Plateau-Rayleigh), and is shaded like a round thread of paint.
    //
    // Mobile budget: one variant, lighting per vertex, two fetches of the
    // liquid's 256px surface texture.
    Properties
    {
        _BaseColor ("Paint Color", Color) = (1, 0, 0, 1)
        _Seed ("Seed", Float) = 0
        [NoScaleOffset] _SurfaceTex ("Surface (RG ripple, B bubbles, A noise)", 2D) = "gray" {}

        [Header(Break up)]
        _BreakStart ("Starts Necking After (s)", Range(0, 2)) = 0.18
        _BreakTime ("Fully Necked After (s more)", Range(0.05, 3)) = 0.45
        _Breakup ("Break Up", Range(0, 1)) = 0.85
        _BeadRate ("Drops Per Second", Range(1, 40)) = 11

        [Header(Surface)]
        _Wobble ("Edge Wobble", Range(0, 0.5)) = 0.14
        _WobbleRate ("Wobble Rate", Range(0.5, 20)) = 4
        _StreakRate ("Streak Rate", Range(0.5, 20)) = 3
        _Streaks ("Streak Contrast", Range(0, 0.5)) = 0.12

        [Header(Light)]
        _EdgeShade ("Edge Shade", Range(0, 1)) = 0.3
        _Highlight ("Highlight", Range(0, 1)) = 0.35
        _HighlightPos ("Highlight Position", Range(-0.9, 0.9)) = -0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PaintFall"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_SurfaceTex);
            SAMPLER(sampler_SurfaceTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Seed;
                float _BreakStart;
                float _BreakTime;
                half _Breakup;
                float _BeadRate;
                half _Wobble;
                float _WobbleRate;
                float _StreakRate;
                half _Streaks;
                half _EdgeShade;
                half _Highlight;
                half _HighlightPos;
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
                half3 light : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                // Lit like the channel floor (see ChannelFillURP), so the paint
                // keeps the exact colour it had in the channel.
                Light mainLight = GetMainLight();
                output.light = mainLight.color * saturate(mainLight.direction.y) + SampleSH(half3(0, 1, 0));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float flight = input.uv.x;
                half across = (half)(input.uv.y * 2.0 - 1.0);   // -1 .. 1
                // When this bit of paint left the outlet: its own, fixed identity.
                float parcel = _Time.y - flight + _Seed;

                half4 slow = SAMPLE_TEXTURE2D(_SurfaceTex, sampler_SurfaceTex,
                    float2(parcel * _WobbleRate * 0.05, 0.31 + _Seed * 0.17));
                half4 fine = SAMPLE_TEXTURE2D(_SurfaceTex, sampler_SurfaceTex,
                    float2(parcel * _StreakRate * 0.05, input.uv.y * 0.35 + 0.5));

                // The thread sways a little and its sides ripple.
                half sway = (slow.a - 0.5h) * 2.0h * _Wobble;
                half ripple = (slow.r - 0.5h) * _Wobble;

                // Necking: the longer it falls, the deeper the waist between
                // beads, until the beads come apart as drops.
                half neck = (half)saturate((flight - _BreakStart) / _BreakTime);
                neck = neck * neck * (3.0h - 2.0h * neck) * _Breakup;
                half beat = (half)(0.5 + 0.5 * cos((parcel * _BeadRate + slow.g * 1.5) * 6.2831853));
                half radius = saturate(1.0h - neck * (1.0h - beat) * 1.15h + ripple);

                half x = across - sway;
                half distance = abs(x);
                half aa = max((half)fwidth(across), 0.002h) * 1.5h;
                half alpha = saturate((radius - distance) / aa + 0.5h);
                clip(alpha - 0.01h);

                // Round thread: normal across the local radius.
                half nx = saturate(distance / max(radius, 0.05h)) * sign(x);
                half nz = sqrt(saturate(1.0h - nx * nx));

                half3 paint = saturate(_BaseColor.rgb * input.light);
                paint *= 1.0h + (fine.a - 0.5h) * 2.0h * _Streaks;
                paint *= lerp(1.0h - _EdgeShade, 1.0h, nz);

                // A long glossy highlight that breaks up along the streaks.
                half spot = saturate(1.0h - abs(nx - _HighlightPos) * 3.5h);
                spot *= spot * (0.6h + 0.8h * fine.b);
                half3 color = paint + spot * _Highlight;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
