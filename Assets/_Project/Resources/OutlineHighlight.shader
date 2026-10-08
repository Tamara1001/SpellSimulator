// Contorno fino de objetos "resaltados" (OutlineHighlight): casco invertido (caras traseras extruidas a lo largo de la
// normal, ancho en metros de mundo). Va en Resources/ para que entre en la build sin tocar assets de render.
Shader "SpellSimulator/OutlineHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.78, 0.45, 1)
        _Width ("Ancho (m)", Float) = 0.004
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Hull"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(i.normalOS));
                o.positionCS = TransformWorldToHClip(posWS + nWS * _Width);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { return half4(_Color.rgb, 1); }
            ENDHLSL
        }
    }
}
