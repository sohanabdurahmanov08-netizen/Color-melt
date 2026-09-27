Shader "ColorMelt/PaintTransition"
{
    // Full-screen paint curtain for scene changes (SceneFader). The paint
    // colour is the Image colour. Distances are in screen widths, measured
    // down from the top of the screen:
    //   _Front - lower edge of the paint; drips run ahead of it.
    //   _Back  - upper edge of the paint while it drains away; streaks of
    //            paint trail behind it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Front ("Front Edge", Float) = -1
        _Back ("Back Edge", Float) = -10
        _Grow ("Drip Growth", Range(0, 1)) = 1
        _Aspect ("Screen Height / Width", Float) = 2.16
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            #define DRIPS 9
            #define STREAKS 8

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float _Front;
            float _Back;
            float _Grow;
            float _Aspect;
            float _Seed;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float n)
            {
                return frac(sin(n) * 43758.5453);
            }

            float Wave(float x, float phase)
            {
                return 0.03 * sin(x * 9.0 + phase) + 0.012 * sin(x * 23.0 + phase * 1.7);
            }

            // How far point p lies inside the pouring paint (negative
            // outside). The body covers everything above the front edge;
            // each drip adds a neck ending in a round drop, and a skirt where
            // it leaves the body. `highlight` marks a glossy stripe down
            // each neck.
            float FrontPaint(float2 p, out float highlight)
            {
                float edge = _Front + Wave(p.x, _Seed * 6.1);
                float inside = edge - p.y;
                highlight = 0.0;
                for (int i = 0; i < DRIPS; i++)
                {
                    float fi = i;
                    float centre = (fi + 0.25 + 0.5 * Hash(_Seed + fi * 1.7)) / DRIPS;
                    float neck = lerp(0.022, 0.042, Hash(_Seed * 1.3 + fi * 3.1));
                    float tip = _Front + lerp(0.08, 0.5, Hash(_Seed * 2.1 + fi * 5.3)) * _Grow;
                    float dx = abs(p.x - centre);

                    float skirt = edge + 0.05 * (1.0 - smoothstep(neck, neck * 3.0, dx)) - p.y;
                    float column = min(neck - dx, tip - p.y);
                    float drop = neck * 1.25 - length(float2(p.x - centre, p.y - tip));
                    inside = max(inside, max(skirt, max(column, drop)));

                    // Only on the hanging part, fading out towards the drop.
                    float hanging = step(edge, p.y) * saturate((tip - p.y) / 0.06);
                    highlight = max(highlight, hanging * (1.0 - saturate(abs(p.x - (centre - neck * 0.4)) / (neck * 0.25))));
                }
                return inside;
            }

            // How far point p lies inside the draining paint: everything
            // below the back edge, plus streaks with round tops trailing
            // above it.
            float BackPaint(float2 p)
            {
                float edge = _Back + Wave(p.x, _Seed * 3.3 + 2.0);
                float inside = p.y - edge;
                for (int i = 0; i < STREAKS; i++)
                {
                    float fi = i + 17.0;
                    float centre = (i + 0.25 + 0.5 * Hash(_Seed + fi * 1.9)) / STREAKS;
                    float width = lerp(0.014, 0.032, Hash(_Seed * 1.7 + fi * 2.3));
                    float top = _Back - lerp(0.08, 0.5, Hash(_Seed * 2.9 + fi * 4.1));
                    float dx = abs(p.x - centre);

                    float skirt = p.y - (edge - 0.04 * (1.0 - smoothstep(width, width * 3.0, dx)));
                    float column = min(width - dx, p.y - top);
                    float cap = width - length(float2(p.x - centre, p.y - top));
                    inside = max(inside, max(skirt, max(column, cap)));
                }
                return inside;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = float2(i.uv.x, (1.0 - i.uv.y) * _Aspect);

                float highlight;
                float front = FrontPaint(p, highlight);
                float inside = min(front, BackPaint(p));

                float aa = max(fwidth(p.x), fwidth(p.y)) * 1.2;
                float alpha = smoothstep(-aa, aa, inside);
                if (alpha <= 0.0)
                    discard;

                // Wet look: faint flow streaks, darker rims on every edge, a soft
                // sheen inside the leading edge and a glossy stripe down each drip.
                fixed3 colour = i.color.rgb;
                colour *= 1.0 + 0.025 * sin(p.x * 38.0 + sin(p.y * 2.5 + _Seed) * 1.5);
                colour *= lerp(1.05, 0.92, saturate(p.y / _Aspect));
                colour *= lerp(0.8, 1.0, saturate(inside / 0.012));
                float gloss = exp(-pow((front - 0.03) / 0.015, 2.0)) * 0.12
                    + highlight * 0.2 * saturate(front / 0.02);
                colour = lerp(colour, fixed3(1.0, 1.0, 1.0), gloss);

                return fixed4(colour, alpha * i.color.a);
            }
            ENDCG
        }
    }

    FallBack Off
}
