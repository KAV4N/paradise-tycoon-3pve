Shader "Custom/ShoreWaves"
{
    Properties
    {
        _SdfTex ("Shore Distance Map", 2D) = "white" {}
        _NoiseTex ("Noise Texture (WaterNoise.png)", 2D) = "gray" {}
        [Toggle] _DebugView ("Debug View (red = outside, blue = inside, green = front)", Float) = 0
        [Toggle] _Overlay ("Overlay mode (set by script on the top quad)", Float) = 0

        [Header(Front Only)]
        _FrontStart ("Front Fade Start (waves fade out above this)", Range(0, 1)) = 0.50
        _FrontEnd ("Front Full (full waves below this)", Range(0, 1)) = 0.36

        [Header(Waves)]
        _WaveCount ("Waves", Range(1, 10)) = 4
        _Speed ("Speed", Range(0, 1)) = 0.22
        _WaveWobble ("Wobble (wavy lines)", Range(0, 1)) = 0.3
        _WobbleScale ("Wobble Size", Range(1, 40)) = 10
        _WaveReach ("Reach (how far from shore)", Range(0.2, 1)) = 0.9
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamOpacity ("Foam Opacity", Range(0, 1)) = 0.8
        _FoamTrail ("Foam Trail (higher = shorter)", Range(0.5, 10)) = 3
        _Lacy ("Lacy Break-up", Range(0, 1)) = 0.6
        _WashDistance ("Wash Up Onto Sand", Range(0.01, 0.4)) = 0.12

        [Header(Shore Line and Glow)]
        _ShoreFoamWidth ("Shore Foam Width", Range(0, 0.2)) = 0.05
        _ShoreFoamOpacity ("Shore Foam Opacity", Range(0, 1)) = 0.7
        _GlowColor ("Shallow Water Color", Color) = (0.45, 0.93, 0.88, 1)
        _GlowOpacity ("Shallow Water Opacity", Range(0, 1)) = 0.45
        _GlowSize ("Shallow Water Size", Range(0.05, 1)) = 0.35
        _GlowBackAmount ("Shallow Water at Back (0 = front only)", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _SdfTex;
            sampler2D _NoiseTex;
            float _DebugView, _Overlay;
            float _FrontStart, _FrontEnd;
            float _WaveCount, _Speed, _WaveWobble, _WobbleScale, _WaveReach, _WashDistance;
            float4 _FoamColor;
            float _FoamOpacity, _FoamTrail, _Lacy;
            float _ShoreFoamWidth, _ShoreFoamOpacity;
            float4 _GlowColor;
            float _GlowOpacity, _GlowSize, _GlowBackAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // noise from the baked texture (same on every device)
            float noiseTex (float2 p)
            {
                float v = tex2D(_NoiseTex, p).r;
                return saturate((v - 0.5) * 2.5 + 0.5); // add contrast
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float T = _Time.y * _Speed;

                // signed distance to the sand: + = out at sea, 0 = shoreline, - = on the island
                float sd = (tex2D(_SdfTex, uv).r - 0.5) * 2.0;
                float d = max(sd, 0.0);

                // only the front of the island (bottom of the picture)
                float front = 1.0 - smoothstep(_FrontEnd, _FrontStart, uv.y);

                if (_DebugView > 0.5) return float4(saturate(sd), front, saturate(-sd), 0.85);

                // noise used to bend the lines and break the foam up
                float2 nuv = uv * float2(1.7, 1.0) * _WobbleScale;
                float n1 = noiseTex(nuv * 0.1 + float2(fmod(_Time.y, 1000.0) * 0.015, 0));
                float n2 = noiseTex(nuv * 0.23 + float2(0, -fmod(_Time.y, 1000.0) * 0.025) + 0.31);

                // 1 on the island, 0 at sea
                float inside = 1.0 - smoothstep(-0.01, 0.01, sd);

                // ---------- rolling waves, moving toward the sand ----------
                float surge = 0.12 * sin(_Time.y * _Speed * 2.5);
                float phase = sd * _WaveCount + T + surge + (n1 - 0.5) * _WaveWobble * 2.0;
                float w = frac(phase);
                float foam = smoothstep(0.0, 0.05, w) * pow(1.0 - w, _FoamTrail);
                foam *= lerp(1.0, saturate(n2 * 1.6), _Lacy * saturate(0.3 + w));

                float reach = 1.0 - smoothstep(_WaveReach * 0.5, _WaveReach, d);
                // on the sand the foam fades out the further it runs up
                float wash = lerp(1.0, smoothstep(-_WashDistance, 0.0, sd), inside);
                float waveFoam = foam * reach * wash * front;

                // ---------- thin foam line right at the sand ----------
                float shoreD = (sd > 0.0) ? sd : -sd * 2.0;
                float shore = 1.0 - smoothstep(0.0, max(_ShoreFoamWidth, 0.001), shoreD);
                float pulse = 0.65 + 0.35 * sin(T * 6.0 + n1 * 6.0);
                shore *= pulse * lerp(1.0, saturate(n2 * 1.8), 0.5) * front;

                // ---------- light shallow water near the sand ----------
                float glow = pow(1.0 - smoothstep(0.0, _GlowSize, d), 1.5);
                float glowMask = lerp(_GlowBackAmount, 1.0, front);

                float aFoam = saturate(waveFoam * _FoamOpacity + shore * _ShoreFoamOpacity);
                float aGlow = glow * _GlowOpacity * glowMask;

                // overlay quad (drawn above the island): only the part on the sand, no glow
                float isOverlay = step(0.5, _Overlay);
                aFoam *= lerp(1.0, inside, isOverlay);
                aGlow *= (1.0 - isOverlay);

                float a = aFoam + aGlow * (1.0 - aFoam);
                float3 rgb = (_FoamColor.rgb * aFoam + _GlowColor.rgb * aGlow * (1.0 - aFoam)) / max(a, 0.0001);

                return float4(rgb, a);
            }
            ENDCG
        }
    }
}
