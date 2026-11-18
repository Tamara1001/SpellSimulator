// Tinta sobre el entorno (piso, paredes, objetos): solo se ve el trazo, con el color de _InkColor y el alfa de la
// textura del lienzo (_MainTex). Transparente, sin escribir profundidad y con un leve empuje hacia la camara.
Shader "SpellSimulator/InkDecal"
{
    Properties
    {
        [MainTexture] _MainTex ("Textura del lienzo", 2D) = "black" {}
        _InkColor ("Color de la tinta", Color) = (0.2, 0.07, 0.1, 1)
        _Glow ("Brillo", Range(0, 3)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -2, -2

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _InkColor;
                float _Glow;
            CBUFFER_END

            // Estereo (VR, single pass instanced): sin estas macros solo se dibuja en el ojo izquierdo.
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // El trazo es lo unico que tiene alfa: sobre fondo transparente, el alfa manda.
                return float4(_InkColor.rgb * _Glow, saturate(tex.a * 1.2));
            }
            ENDHLSL
        }
    }
}
