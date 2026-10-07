// Marca en el suelo (crater, quemadura, charco): disco procedural con borde irregular sobre un quad, sin textura.
// _BaseColor y _Fade por MaterialPropertyBlock. _Rim > 0 aclara el borde (labio del crater).
Shader "SpellSimulator/GroundMark"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.2, 0.15, 0.1, 0.8)
        _RimColor ("Rim Color", Color) = (0.55, 0.45, 0.35, 1)
        _Rim ("Rim", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                float _Rim;
                float _Fade;
                float _Seed;
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

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float angle = atan2(p.y, p.x);
                // Borde irregular: suma de senos con fase por marca.
                float wobble = 0.08 * sin(angle * 5 + _Seed) + 0.05 * sin(angle * 9 - _Seed * 1.7);
                float d = length(p) / (0.85 + wobble);
                float body = 1 - smoothstep(0.75, 1.0, d);
                float rim = smoothstep(0.6, 0.85, d) * (1 - smoothstep(0.85, 1.0, d)) * _Rim;
                half4 c = lerp(_BaseColor, _RimColor, saturate(rim * 2));
                c.a = max(body * _BaseColor.a, rim * _RimColor.a) * _Fade;
                return c;
            }
            ENDHLSL
        }
    }
}
