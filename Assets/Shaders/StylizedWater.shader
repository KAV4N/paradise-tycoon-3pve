Shader "Custom/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color (near island)", Color) = (0.22, 0.76, 0.80, 1)
        _DeepColor ("Deep Color (screen edges)", Color) = (0.10, 0.58, 0.67, 1)
        _PatternColor ("Light Pattern Color", Color) = (0.50, 0.90, 0.92, 1)
        _Tiling ("Pattern Tiling (X, Y)", Vector) = (12, 7, 0, 0)
        _Speed ("Animation Speed", Range(0, 2)) = 0.35
        _PatternStrength ("Pattern Strength", Range(0, 1)) = 0.35
        _PatternSoftness ("Pattern Softness", Range(0.05, 0.6)) = 0.25
        _WaveDistort ("Wave Distortion", Range(0, 0.1)) = 0.02
        _GradientSize ("Gradient Size", Range(0.2, 2)) = 1.0
        _SparkleAmount ("Sparkle Amount", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _PatternColor;
            float4 _Tiling;
            float _Speed;
            float _PatternStrength;
            float _PatternSoftness;
            float _WaveDistort;
            float _GradientSize;
            float _SparkleAmount;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 hash22 (float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Animated voronoi: distance to the nearest moving cell point
            float voronoi (float2 x, float t)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float d = 8.0;
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 o = hash22(n + g);
                        o = 0.5 + 0.5 * sin(t + 6.2831 * o);
                        float2 r = g + o - f;
                        d = min(d, dot(r, r));
                    }
                }
                return sqrt(d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv;

                // gentle wobble so the pattern feels like moving water
                float2 wob = float2(sin(uv.y * 10.0 + t * 2.0), cos(uv.x * 10.0 + t * 1.7)) * _WaveDistort;

                // base color: light in the middle, darker toward the edges
                float d = distance(uv, float2(0.5, 0.5)) * 2.0;
                float g = smoothstep(0.0, _GradientSize, d);
                fixed3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, g);

                // two overlapping voronoi layers = soft organic light patches
                float v1 = voronoi((uv + wob) * _Tiling.xy, t);
                float v2 = voronoi((uv - wob) * _Tiling.xy * 0.6 + 7.3, -t * 0.8);
                float s = _PatternSoftness;
                float p1 = smoothstep(0.55, 0.55 - s, v1);
                float p2 = smoothstep(0.55, 0.55 - s, v2);
                float pattern = p1 * p2;
                col = lerp(col, _PatternColor.rgb, pattern * _PatternStrength);

                // tiny sparkles
                float sp = voronoi(uv * _Tiling.xy * 3.0, t * 3.0);
                float spark = smoothstep(0.07, 0.0, sp);
                col += spark * _SparkleAmount * 0.35;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
