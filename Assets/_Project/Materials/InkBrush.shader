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
                half4 _InkColor;
                half _Softness;
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

            half4 frag(Varyings input) : SV_Target
            {
                half d = length(input.uv - 0.5h) * 2.0h;
                half a = saturate((1.0h - d) / max(_Softness, 0.001h)) * _InkColor.a;
                // Blanco donde no hay tinta (Min no cambia el fondo), color de tinta donde si.
                half3 color = lerp(half3(1, 1, 1), _InkColor.rgb, a);
                return half4(color, a);
            }
            ENDHLSL
        }
    }
}
