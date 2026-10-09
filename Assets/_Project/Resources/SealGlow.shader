// Brillo del anillo de un sello mientras carga: banda anular aditiva, unlit, con nucleo en la linea de tinta y halo suave.
// uv.y va de 0 (borde interior) a 1 (borde exterior); la tinta esta en 0.5. Vive en Resources para entrar siempre al build.
// Sin half (KI-104: WebGL), compatible con SRP Batcher y stereo instancing (Quest).
Shader "SpellSimulator/SealGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.5, 0.2, 1)
        _Intensity ("Intensity", Float) = 1
        _Pulse ("Pulse", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Pulse;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 0 sobre la linea de tinta, 1 en los bordes de la banda.
                float d = abs(input.uv.y * 2.0 - 1.0);
                float core = 1.0 - smoothstep(0.0, 0.35, d);
                float halo = 1.0 - d;
                halo *= halo;
                float pulse = lerp(0.7, 1.3, _Pulse);
                float glow = (core * 0.9 + halo * 0.55) * _Intensity * pulse;
                // El nucleo tira levemente a blanco para que lea como luz y no como pintura.
                float3 col = _Color.rgb * glow + core * core * 0.2 * _Intensity;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
