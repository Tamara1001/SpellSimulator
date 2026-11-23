// Agua estilizada para URP (apta Quest): olas suaves por vertice, ondas por impacto calculadas analiticamente en el
// fragmento (sin render textures) y espuma en los anillos. WaterVolume carga hasta 16 ondas en _Ripples:
// (x, z en mundo, tiempo de inicio en _Time.y, fuerza).
Shader "SpellSimulator/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.35, 0.75, 0.8, 0.65)
        _DeepColor ("Deep", Color) = (0.08, 0.3, 0.45, 0.9)
        _FoamColor ("Foam", Color) = (0.95, 0.98, 1, 1)
        _WaveHeight ("Wave Height", Range(0, 0.2)) = 0.03
        _WaveLength ("Wave Length", Range(0.5, 10)) = 2.5
        _WaveSpeed ("Wave Speed", Range(0, 3)) = 0.6
        _RippleSpeed ("Ripple Speed (m/s)", Range(0.2, 5)) = 1.4
        _RippleWidth ("Ripple Width (m)", Range(0.02, 0.5)) = 0.12
        _RippleLife ("Ripple Life (s)", Range(0.3, 5)) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_RIPPLES 16
            #define RIPPLES_MID 8
            #define RIPPLES_FAR 4

            // Tramos de distancia para culling de ondas analiticas (VFX-05).
            // - Cerca (<= kDistMid): hasta 16 ondas; ondas 8..15 se atenuan suavemente entre kDistMidFade y kDistMid.
            // - Medio (kDistMid..kDistFar): 8 ondas; ondas 4..7 se atenuan entre kDistFarFade y kDistFar.
            // - Lejos (kDistFar..kDistCull): 4 ondas; ondas 0..3 se atenuan entre kDistCullFade y kDistCull.
            // - Fuera (> kDistCull): 0 ondas (corte completo de bucle, costo cero).
            static const float kDistNear     = 12.0; // Distancia con 16 ondas a 100% de amplitud (identico al original)
            static const float kDistMidFade  = 15.0; // Inicio atenuacion ondas 8..15
            static const float kDistMid      = 20.0; // Corte a tramo medio (8 ondas)
            static const float kDistFarFade  = 26.0; // Inicio atenuacion ondas 4..7
            static const float kDistFar      = 32.0; // Corte a tramo lejano (4 ondas)
            static const float kDistCullFade = 38.0; // Inicio atenuacion ondas 0..3
            static const float kDistCull     = 45.0; // Culling total (0 ondas)

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _RippleSpeed;
                float _RippleWidth;
                float _RippleLife;
            CBUFFER_END

            float4 _Ripples[MAX_RIPPLES];
            float _RippleCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Dos trenes de olas cruzados: altura y pendiente.
            float2 Waves(float2 xz, out float height)
            {
                float k = 6.2831853 / _WaveLength;
                float t = _Time.y * _WaveSpeed;
                float a = xz.x * k + t;
                float b = (xz.x * 0.6 + xz.y * 0.8) * k * 1.3 - t * 1.2;
                height = _WaveHeight * (sin(a) + 0.6 * sin(b));
                return _WaveHeight * k * float2(cos(a) + 0.6 * 0.6 * 1.3 * cos(b), 0.6 * 0.8 * 1.3 * cos(b));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float h;
                Waves(ws.xz, h);
                ws.y += h;
                output.positionWS = ws;
                output.positionCS = TransformWorldToHClip(ws);
                return output;
            }

            // Evalua una onda de impacto individual con peso de atenuacion por distancia.
            void AccumulateRipple(int i, int count, float3 positionWS, float weight, inout float foam, inout float2 slope)
            {
                if (i >= count || weight <= 0.0001) return;
                float4 r = _Ripples[i];
                float age = _Time.y - r.z;
                if (age < 0 || age > _RippleLife) return;
                float2 delta = positionWS.xz - r.xy;
                float d = length(delta);
                float front = age * _RippleSpeed;
                float ring = exp(-pow((d - front) / _RippleWidth, 2));
                float fade = (1 - age / _RippleLife) * r.w * weight;
                foam += ring * fade;
                slope += (d > 1e-3 ? delta / d : 0) * ring * fade * 0.6 * cos((d - front) * 25);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float h;
                float2 slope = Waves(input.positionWS.xz, h);

                // Ondas de impacto: anillos con culling por distancia y atenuacion suave (VFX-05).
                float foam = 0;
                int count = (int)_RippleCount;
                float camDist = distance(_WorldSpaceCameraPos, input.positionWS);

                [branch]
                if (count > 0 && camDist < kDistCull)
                {
                    [branch]
                    if (camDist > kDistFar)
                    {
                        // Tramo Lejano: 4 ondas con cota constante para unroll movil
                        float wFade = saturate((kDistCull - camDist) / (kDistCull - kDistCullFade));
                        [unroll]
                        for (int i = 0; i < RIPPLES_FAR; i++)
                        {
                            AccumulateRipple(i, count, input.positionWS, wFade, foam, slope);
                        }
                    }
                    else if (camDist > kDistMid)
                    {
                        // Tramo Medio: 8 ondas con cota constante para unroll movil
                        float wFade = saturate((kDistFar - camDist) / (kDistFar - kDistFarFade));
                        [unroll]
                        for (int i = 0; i < RIPPLES_MID; i++)
                        {
                            float w = (i >= RIPPLES_FAR) ? wFade : 1.0;
                            AccumulateRipple(i, count, input.positionWS, w, foam, slope);
                        }
                    }
                    else
                    {
                        // Tramo Cercano: 16 ondas con cota constante; identico al original cerca
                        float wFade = saturate((kDistMid - camDist) / (kDistMid - kDistMidFade));
                        [unroll]
                        for (int i = 0; i < MAX_RIPPLES; i++)
                        {
                            float w = (i >= RIPPLES_MID) ? wFade : 1.0;
                            AccumulateRipple(i, count, input.positionWS, w, foam, slope);
                        }
                    }
                }

                float3 normal = normalize(float3(-slope.x, 1, -slope.y));
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1 - saturate(dot(normal, view)), 3);
                half4 col = lerp(_ShallowColor, _DeepColor, saturate(fresnel * 1.5 + 0.2));

                Light light = GetMainLight();
                float3 halfDir = normalize(light.direction + view);
                float spec = pow(saturate(dot(normal, halfDir)), 120) * 1.5;
                col.rgb += spec * light.color;
                col = lerp(col, _FoamColor, saturate(foam));
                return col;
            }
            ENDHLSL
        }
    }
}
