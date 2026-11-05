Shader "Custom/RealisticWater"
{
    Properties
    {
        [Header(Depth Colors)]
        _ShallowColor ("Shallow (near island)", Color) = (0.30, 0.80, 0.78, 1)
        _MidColor ("Mid", Color) = (0.07, 0.52, 0.64, 1)
        _DeepColor ("Deep (far from island)", Color) = (0.02, 0.25, 0.42, 1)

        [Header(Island Area)]
        _IslandCenter ("Island Center (UV)", Vector) = (0.5, 0.52, 0, 0)
        _IslandRadius ("Island Radius (X, Y)", Vector) = (0.36, 0.30, 0, 0)
        _ShallowStart ("Shallow Start", Range(0, 1.5)) = 0.7
        _ShallowEnd ("Shallow End (deep begins)", Range(0.5, 3)) = 1.9

        [Header(Waves)]
        _Tiling ("Wave Tiling (X, Y)", Vector) = (10, 6, 0, 0)
        _Speed ("Speed", Range(0, 1)) = 0.12
        _NormalStrength ("Wave Strength", Range(0, 2)) = 0.6

        [Header(View)]
        _ViewAngle ("View Angle (90 = top-down, lower = more tilted)", Range(15, 90)) = 60
        _HorizonHaze ("Distance Haze (top of screen)", Range(0, 1)) = 0.2

        [Header(Light)]
        _SunAngle ("Sun Direction (degrees)", Range(0, 360)) = 45
        _SunHeight ("Sun Height (lower = more glitter)", Range(0.3, 3)) = 1.3
        _SpecPower ("Glint Sharpness", Range(20, 400)) = 160
        _SpecIntensity ("Glint Intensity", Range(0, 3)) = 0.9
        _SkyColor ("Sky Reflection Color", Color) = (0.75, 0.90, 1.0, 1)
        _SkyAmount ("Sky Reflection Amount", Range(0, 1)) = 0.25

        [Header(Caustics)]
        _CausticColor ("Caustic Color", Color) = (0.85, 1.0, 0.95, 1)
        _CausticIntensity ("Caustic Intensity", Range(0, 2)) = 0.5
        _CausticSharpness ("Caustic Sharpness", Range(2, 30)) = 12
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

            float4 _ShallowColor, _MidColor, _DeepColor;
            float4 _IslandCenter, _IslandRadius;
            float _ShallowStart, _ShallowEnd;
            float4 _Tiling;
            float _Speed, _NormalStrength;
            float _ViewAngle, _HorizonHaze;
            float _SunAngle, _SunHeight, _SpecPower, _SpecIntensity;
            float4 _SkyColor;
            float _SkyAmount;
            float4 _CausticColor;
            float _CausticIntensity, _CausticSharpness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm (float2 p)
            {
                float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += a * vnoise(p);
                    p = mul(rot, p) * 2.0;
                    a *= 0.5;
                }
                return v / 0.875;
            }

            // two scrolling noise layers = rolling wave height
            float waveHeight (float2 p, float t)
            {
                float a = fbm(p * 0.8 + float2(t, t * 0.6));
                float b = fbm(p * 1.6 + float2(-t * 0.8, t * 1.1) + 11.7);
                return a * 0.6 + b * 0.4;
            }

            float4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 uv = i.uv;
                float el = radians(_ViewAngle);
                float sinEl = max(sin(el), 0.2);
                float2 p = uv * _Tiling.xy;
                p.y /= sinEl; // tilted view squashes waves vertically, like real perspective

                // ---------- wave normal from height field ----------
                float e = 0.03;
                float h0 = waveHeight(p, t);
                float hx = waveHeight(p + float2(e, 0), t);
                float hy = waveHeight(p + float2(0, e), t);
                float2 slope = float2(hx - h0, hy - h0) / e;
                float3 N = normalize(float3(-slope.x * _NormalStrength, -slope.y * _NormalStrength, 1.0));

                // ---------- depth (shallow near island, deep far away) ----------
                float2 q = (uv - _IslandCenter.xy) / _IslandRadius.xy;
                float dist = length(q);
                dist += (fbm(uv * 4.0 + t * 0.3) - 0.5) * 0.35; // organic edge
                float depth = smoothstep(_ShallowStart, _ShallowEnd, dist);

                float3 col = lerp(_ShallowColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.55, depth));
                col = lerp(col, _DeepColor.rgb, smoothstep(0.45, 1.0, depth));

                // ---------- caustics on the shallow seabed ----------
                float2 cq = p * 0.7;
                float r1 = 1.0 - abs(2.0 * fbm(cq + float2(t * 0.8, t * 0.5)) - 1.0);
                float r2 = 1.0 - abs(2.0 * fbm(cq * 1.3 + float2(-t * 0.6, t * 0.7) + 5.2) - 1.0);
                float caustic = pow(saturate(r1 * r2), _CausticSharpness);
                float shallowMask = pow(1.0 - depth, 2.0);
                col += _CausticColor.rgb * caustic * _CausticIntensity * shallowMask;

                // ---------- lighting ----------
                float ang = radians(_SunAngle);
                float3 L = normalize(float3(cos(ang), sin(ang), _SunHeight));
                float3 V = normalize(float3(0, -cos(el), sin(el)));
                float3 H = normalize(L + V);

                // soft shading from wave slopes
                col *= 0.92 + 0.16 * saturate(dot(N, L));

                // sun glints
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecIntensity;
                col += spec * float3(1.0, 0.98, 0.92);

                // faint sky reflection on tilted wave faces
                // fresnel: the lower the view angle, the more sky the water reflects
                float ndv = saturate(dot(N, V));
                float fres = pow(1.0 - ndv, 3.0) * 3.0;
                col = lerp(col, _SkyColor.rgb, saturate(fres * _SkyAmount * 4.0));

                // distance haze toward the top of the screen (only when tilted)
                float haze = smoothstep(0.35, 1.0, uv.y) * _HorizonHaze * (1.0 - sinEl);
                col = lerp(col, _SkyColor.rgb * 0.85, saturate(haze));

                return float4(col, 1);
            }
            ENDCG
        }
    }
}
