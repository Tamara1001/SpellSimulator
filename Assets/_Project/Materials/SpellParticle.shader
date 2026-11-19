// Particula de hechizo para URP: disco suave procedural (sin textura) tenido por el color de vertice.
// _Additive = 1: brillo aditivo (fuego, luz). 0: alfa (agua, tierra, viento, humo). Barato en Quest.
// _Shape: 0 disco suave, 1 gota (borde claro y brillo), 2 bocanada toon (polvo: arriba clara, borde nitido), 3 hoja,
// 4 cinta (estelas Trails: u a lo largo, v a lo ancho), 5 llama (lengua con ruido que se erosiona al envejecer: el alfa
// del color a lo largo de la vida es la edad), 6 humo (bocanada irregular blanda), 7 rizo de aire (arco en C que gira). Rama por uniforme: un solo shader para todos los looks.
Shader "SpellSimulator/SpellParticle"
{
    Properties
    {
        [Toggle] _Additive ("Additive", Float) = 1
        _Softness ("Softness", Range(0.01, 1)) = 0.6
        _Intensity ("Intensity", Range(0, 8)) = 1.5
        _Shape ("Shape", Float) = 0
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SrcBlend ("Src", Float) = 5
        [HideInInspector] _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Additive;
                float _Softness;
                float _Intensity;
                float _Shape;
                float4 _RimColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 q)
            {
                q = frac(q * float2(123.34, 456.21));
                q += dot(q, q + 45.32);
                return frac(q.x * q.y);
            }

            float VNoise(float2 q)
            {
                float2 i = floor(q);
                float2 f = frac(q);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 q)
            {
                return VNoise(q) * 0.6 + VNoise(q * 2.07 + 5.3) * 0.4;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float d = length(p);
                float mask = saturate((1.0 - d) / _Softness);
                mask *= mask;
                float4 c = input.color;
                if (_Shape > 0.5)
                {
                    if (_Shape < 1.5)
                    {
                        // Gota: cuerpo translucido, borde claro (espuma) y un brillo arriba a la izquierda.
                        mask = saturate((1.0 - d) / 0.12);
                        float rim = smoothstep(0.55, 0.9, d);
                        float spot = 1.0 - smoothstep(0.0, 0.28, length(p - float2(-0.3, 0.3)));
                        c.rgb = lerp(c.rgb, _RimColor.rgb, saturate(rim * 0.55 + spot));
                        c.a *= 0.55 + 0.45 * saturate(rim + spot);
                    }
                    else if (_Shape < 2.5)
                    {
                        // Bocanada toon: borde nitido, luz arriba, sombra abajo y un filo claro.
                        float edge = 0.82 + 0.04 * sin(atan2(p.y, p.x) * 6.0);
                        mask = saturate((edge - d) / 0.08);
                        float top = saturate(p.y * 0.5 + 0.5);
                        c.rgb *= 0.78 + 0.35 * top;
                        c.rgb = lerp(c.rgb, _RimColor.rgb, smoothstep(edge - 0.16, edge - 0.04, d) * 0.45 * top);
                    }
                    else if (_Shape < 3.5)
                    {
                        // Hoja: lente con punta, nervio central mas oscuro.
                        float w = 0.48 * (1.0 - p.y * p.y);
                        mask = saturate((w - abs(p.x)) / 0.06);
                        c.rgb *= 1.0 - 0.3 * (1.0 - smoothstep(0.0, 0.06, abs(p.x)));
                    }
                    else if (_Shape < 4.5)
                    {
                        // Cinta de estela: centro brillante, se afina hacia la cola.
                        float across = abs(input.uv.y * 2.0 - 1.0);
                        float along = smoothstep(0.0, 0.12, input.uv.x) * (1.0 - input.uv.x);
                        mask = saturate((1.0 - across) / 0.5) * along;
                        c.rgb = lerp(c.rgb, _RimColor.rgb, saturate(1.0 - across * 2.5));
                    }
                    else if (_Shape < 5.5)
                    {
                        // Llama: gota alargada hacia arriba con bordes de ruido que suben; al envejecer (alfa baja) se erosiona
                        // en jirones. Nucleo caliente (_RimColor) en el centro, color del gradiente en los bordes.
                        float2 q = float2(p.x * (1.0 + 0.55 * (p.y * 0.5 + 0.5)), p.y * 0.85 - 0.1);
                        float body = 1.0 - length(q);
                        float n = Fbm(p * 2.4 + float2(0.0, -_Time.y * 2.6));
                        float age = 1.0 - saturate(input.color.a);
                        mask = smoothstep(0.0, 0.32, body + (n - 0.5) * 0.9 - age * 0.55);
                        float heat = saturate(body * 1.3 + (n - 0.5) * 0.3 - age * 0.6);
                        c.rgb = lerp(c.rgb, _RimColor.rgb, heat * heat);
                        c.a = saturate(c.a * 1.4);
                    }
                    else if (_Shape < 6.5)
                    {
                        // Humo: bocanada irregular blanda, mas densa abajo y deshilachada arriba.
                        float n = Fbm(p * 2.0 + float2(3.7, -_Time.y * 0.6));
                        float body = 1.0 - d * (0.85 + 0.3 * n);
                        mask = smoothstep(0.0, 0.55, body + (n - 0.5) * 0.5);
                        c.rgb *= 0.82 + 0.3 * saturate(p.y * 0.5 + 0.5) * n;
                    }
                    else
                    {
                        // Rizo de aire: arco en C de trazo que se afina en las puntas; con giro y alfa baja se ven remolinos.
                        float ang = atan2(p.y, p.x);
                        float ring = 1.0 - abs(d - 0.62) / 0.17;
                        float gap = smoothstep(-0.2, 1.1, cos(ang - 0.6));
                        mask = saturate(ring * 1.4) * gap;
                        mask *= mask;
                        c.rgb = lerp(c.rgb, _RimColor.rgb, saturate(ring) * 0.5);
                    }
                }
                if (_Additive > 0.5)
                    return float4(c.rgb * c.a * mask * _Intensity, 1);
                return float4(c.rgb, c.a * mask);
            }
            ENDHLSL
        }
    }
}
