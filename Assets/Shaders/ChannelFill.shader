Shader "ColorMelt/ChannelFill"
{
    // Требование к модели: UV.x должен идти от 0 (вход канала) до 1 (выход канала)
    // вдоль всей длины трубы/горки. UV.y — поперёк, не используется для заливки.
    Properties
    {
        _EmptyColor ("Empty Channel Color", Color) = (0.85, 0.85, 0.85, 1)
        _FillColor  ("Fill Color", Color) = (1, 0, 0, 1)
        _FillAmount ("Fill Amount", Range(0,1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.03
        _EdgeGlow ("Edge Glow Color", Color) = (1, 1, 1, 1)
        _EdgeGlowWidth ("Edge Glow Width", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _EmptyColor;
            fixed4 _FillColor;
            float _FillAmount;
            float _EdgeSoftness;
            fixed4 _EdgeGlow;
            float _EdgeGlowWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Прогресс вдоль канала берём из UV.x, размеченного на модели
                float progress = i.uv.x;

                // Плавная граница между залитой и пустой частью канала
                float fillMask = smoothstep(_FillAmount, _FillAmount - _EdgeSoftness, progress);

                fixed4 col = lerp(_EmptyColor, _FillColor, fillMask);

                // Лёгкое свечение прямо на границе потока — эффект "передового края" жидкости
                float edgeDist = abs(progress - _FillAmount);
                float edgeMask = 1 - smoothstep(0, _EdgeGlowWidth, edgeDist);
                edgeMask *= step(progress, _FillAmount + _EdgeGlowWidth); // не светить в пустой части

                col.rgb = lerp(col.rgb, _EdgeGlow.rgb, edgeMask * _EdgeGlow.a);

                return col;
            }
            ENDCG
        }
    }
}
