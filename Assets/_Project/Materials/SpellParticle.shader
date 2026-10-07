// Particula de hechizo para URP: disco suave procedural (sin textura) tenido por el color de vertice.
// _Additive = 1: brillo aditivo (fuego, luz). 0: alfa (agua, tierra, viento, humo). Barato en Quest.
Shader "SpellSimulator/SpellParticle"
{
    Properties
    {
        [Toggle] _Additive ("Additive", Float) = 1
        _Softness ("Softness", Range(0.01, 1)) = 0.6
        _Intensity ("Intensity", Range(0, 8)) = 1.5
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

            half4 frag(Varyings input) : SV_Target
            {
                float d = length(input.uv * 2.0 - 1.0);
                float mask = saturate((1.0 - d) / _Softness);
                mask *= mask;
                half4 c = input.color;
                if (_Additive > 0.5)
                    return half4(c.rgb * c.a * mask * _Intensity, 1);
                return half4(c.rgb, c.a * mask);
            }
            ENDHLSL
        }
    }
}
