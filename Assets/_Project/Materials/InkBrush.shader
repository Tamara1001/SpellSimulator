// Pincel de tinta del lienzo: un disco suave dibujado con BlendOp Min (color) y Max (alfa). Al superponer sellos el trazo
// queda liso y de un solo tono (antes cada sello semitransparente se sumaba y se veian "circulos uno encima del otro").
// Sobre papel (fondo opaco claro) el Min oscurece solo donde hay tinta; sobre una calcomania transparente el Max deja el
// alfa del trazo (el color lo pone el shader InkDecal).
Shader "SpellSimulator/InkBrush"
{
    Properties
    {
        _InkColor ("Color de la tinta", Color) = (0.368, 0.085, 0.085, 1)
        _Softness ("Suavidad del borde", Range(0.02, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        BlendOp Min, Max
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _InkColor;
                float _Softness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float d = length(input.uv - 0.5) * 2.0;
                float a = saturate((1.0 - d) / max(_Softness, 0.001)) * _InkColor.a;
                // Blanco donde no hay tinta (Min no cambia el fondo), color de tinta donde si.
                float3 color = lerp(float3(1, 1, 1), _InkColor.rgb, a);
                return float4(color, a);
            }
            ENDHLSL
        }
    }
}
