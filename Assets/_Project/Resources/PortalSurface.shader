// Disco de un portal circular (estilo Witch Hat Atelier): pelicula oscura con vetas tenues cuando duerme y un remolino
// luminoso con borde brillante cuando se abre. uv: centro en (0.5, 0.5), radio 0.5 = borde del disco (la malla puede venir
// recortada por el piso). Lo animan PortalVisuals por MaterialPropertyBlock (_State, _Open, _Rim, _Tint) y _Time.
// Unlit, alpha simple, ZWrite Off, dos caras. Sin half (KI-104: WebGL), compatible con SRP Batcher y stereo instancing (Quest).
// Vive en Resources para entrar siempre al build.
Shader "SpellSimulator/PortalSurface"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (0.6, 0.5, 1, 1)
        _Dark ("Dark", Color) = (0.06, 0.05, 0.11, 1)
        _State ("State (0 dormido, 1 cargando, 2 abierto, 3 enfriando)", Float) = 0
        _Open ("Open", Range(0, 1)) = 0
        _Rim ("Rim", Range(0, 2)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
                float4 _Tint;
                float4 _Dark;
                float _State;
                float _Open;
                float _Rim;
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
                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _Time.y;
                float open = saturate(_Open);

                // Dormido: vetas finas que giran muy despacio, apenas teñidas.
                float vein = 1.0 - abs(sin(ang * 3.0 + r * 7.0 + sin(r * 11.0 + t * 0.25) * 0.8));
                vein = vein * vein * vein * vein * vein * vein;
                float3 dormant = _Dark.rgb + _Tint.rgb * vein * 0.18;

                // Abierto: espiral logaritmica que gira hacia el centro, con nucleo claro.
                float speed = lerp(0.4, 1.8, open);
                float spiral = 0.5 + 0.5 * sin(ang * 3.0 + log(r + 0.04) * 6.0 + t * 2.2 * speed);
                float spiral2 = 0.5 + 0.5 * sin(ang * 5.0 - log(r + 0.04) * 4.0 - t * 1.3 * speed);
                float core = 1.0 - smoothstep(0.0, 0.85, r);
                float swirl = spiral * 0.7 + spiral2 * 0.3;
                float3 lit = _Tint.rgb * (0.3 + swirl * (0.55 + core * 0.8)) + core * core * 0.55;

                float3 col = lerp(dormant, lit, open);

                // Cargando: ondas que viajan hacia adentro (solo en _State = 1).
                float charging = step(0.5, _State) * step(_State, 1.5);
                float rings = 0.5 + 0.5 * sin(r * 20.0 + t * 7.0);
                rings = rings * rings * rings * rings;
                col += _Tint.rgb * rings * charging * _Rim * 0.35;

                // Borde luminoso.
                float rim = smoothstep(0.72, 0.99, r);
                col += _Tint.rgb * rim * rim * _Rim * 1.4;

                float alpha = lerp(0.85, 0.95, open) * (1.0 - smoothstep(0.985, 1.0, r));
                return float4(col, alpha);
            }
            ENDHLSL
        }
    }
}
