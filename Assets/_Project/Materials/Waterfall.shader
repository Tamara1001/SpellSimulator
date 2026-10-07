// Cascada estilizada: cinta transparente con vetas que bajan (ruido procedural desplazado en V), espuma al pie y en
// los bordes. UV: u = ancho (0-1), v = largo (0 arriba, 1 abajo). Sin texturas; apta Quest.
Shader "SpellSimulator/Waterfall"
{
    Properties
    {
        _Color ("Water", Color) = (0.55, 0.8, 0.95, 0.75)
        _FoamColor ("Foam", Color) = (0.95, 0.98, 1, 0.95)
        _Speed ("Speed", Range(0, 4)) = 1.2
        _Streaks ("Streak Density", Range(1, 40)) = 14
        _Length ("Length (m, para escalar las vetas)", Float) = 25
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FoamColor;
                float _Speed;
                float _Streaks;
                float _Length;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fog : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y * _Speed;
                // Vetas largas que caen: ruido estirado en V que se desplaza hacia abajo.
                float streak = Noise(float2(uv.x * _Streaks, uv.y * _Length * 0.15 - t * 3));
                streak = smoothstep(0.55, 0.8, streak);
                float foamBottom = smoothstep(0.82, 1.0, uv.y);
                float edges = smoothstep(0.35, 0.5, abs(uv.x - 0.5));
                float foam = saturate(streak * 0.7 + foamBottom + edges * 0.5);
                half4 c = lerp(_Color, _FoamColor, foam);
                c.a *= (1 - edges * 0.6) * smoothstep(0.0, 0.05, uv.y);
                c.rgb = MixFog(c.rgb, input.fog);
                return c;
            }
            ENDHLSL
        }
    }
}
