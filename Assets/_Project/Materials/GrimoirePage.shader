// Pagina del grimorio: pergamino procedural (textura generada por GrimoireSetup) multiplicado por la tinta del lienzo.
// _BaseMap es la RenderTexture del DrawingCanvas (fondo blanco + trazos); en la pagina sin lienzo queda en blanco.
// _Mirror espeja solo el pergamino (la sombra del lomo queda del lado de adentro en ambas paginas).
// Iluminacion: lambert suave con la luz principal + ambiente, para que no "brille" de noche ni se vea plano de dia.
Shader "SpellSimulator/GrimoirePage"
{
    Properties
    {
        [MainTexture] _BaseMap ("Tinta (lienzo)", 2D) = "white" {}
        _Parchment ("Pergamino", 2D) = "white" {}
        _Tint ("Tinte", Color) = (1, 1, 1, 1)
        _Mirror ("Espejar pergamino", Float) = 0
        _Lit ("Cuanto lo afecta la luz", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Parchment); SAMPLER(sampler_Parchment);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Parchment_ST;
                float4 _Tint;
                float _Mirror;
                float _Lit;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 ink = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                float2 puv = i.uv;
                puv.x = lerp(puv.x, 1.0 - puv.x, _Mirror);
                float4 paper = SAMPLE_TEXTURE2D(_Parchment, sampler_Parchment, puv);
                Light light = GetMainLight();
                float ndl = saturate(abs(dot(normalize(i.normalWS), light.direction)));
                float3 lightCol = saturate(light.color * ndl + SampleSH(normalize(i.normalWS)));
                float3 lit = lerp(float3(1, 1, 1), saturate(lightCol * 0.6 + 0.45), _Lit);
                return float4(paper.rgb * ink.rgb * _Tint.rgb * lit, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
