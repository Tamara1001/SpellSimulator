// Shader de binarizacion para preprocesamiento de lienzos/trazos en URP.
// Convertido de Built-in CG a HLSL URP con soporte para SRP Batcher y stereo instancing (VR / Meta Quest).
Shader "Custom/Binarize"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Luminance Threshold", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Binarize"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Threshold;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Luminancia perceptual segun estandar Rec. 601
                half lum = dot(col.rgb, half3(0.299, 0.587, 0.114));

                // Binarizacion: tinta oscura sobre fondo claro.
                // Si lum < _Threshold (tinta), se invierte a blanco (1.0).
                // Si lum >= _Threshold (fondo), se invierte a negro (0.0).
                half bw = lum < _Threshold ? 1.0 : 0.0;

                return half4(bw, bw, bw, 1.0);
            }
            ENDHLSL
        }
    }
}
