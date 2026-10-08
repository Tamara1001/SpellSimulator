// Particula de hechizo para URP: disco suave procedural (sin textura) tenido por el color de vertice.
// _Additive = 1: brillo aditivo (fuego, luz). 0: alfa (agua, tierra, viento, humo). Barato en Quest.
// _Shape: 0 disco suave, 1 gota (borde claro y brillo), 2 bocanada toon (polvo: arriba clara, borde nitido), 3 hoja,
// 4 cinta (estelas Trails: u a lo largo, v a lo ancho). Rama por uniforme: un solo shader para todos los looks.
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
                    else
                    {
                        // Cinta de estela: centro brillante, se afina hacia la cola.
                        float across = abs(input.uv.y * 2.0 - 1.0);
                        float along = smoothstep(0.0, 0.12, input.uv.x) * (1.0 - input.uv.x);
                        mask = saturate((1.0 - across) / 0.5) * along;
                        c.rgb = lerp(c.rgb, _RimColor.rgb, saturate(1.0 - across * 2.5));
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
