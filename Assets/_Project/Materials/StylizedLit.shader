// Low-poly estilizado para URP: color por vertice, luz principal en bandas suaves (toon), sombras de URP, ambiente
// en tres tonos y niebla. Las normales planas vienen del mesh (vertices sin compartir). Apto Quest.
// Cel: _RimSharpness 0 = rim suave de siempre, 1 = rim recortado solo del lado iluminado. Pases DepthNormals propios
// para el contorno por aristas (OutlineRendererFeature). Con la keyword global _GRASS_BURN pinta el suelo quemado del
// incendio de pasto (BurntGround.hlsl).
Shader "SpellSimulator/StylizedLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.8, 0.78, 0.9, 1)
        _Bands ("Light Bands", Range(1, 6)) = 3
        _Softness ("Band Softness", Range(0.01, 0.5)) = 0.12
        _RimStrength ("Rim", Range(0, 1)) = 0.15
        // 0 = rim suave de siempre (pow 4); 1 = rim cel: corte nitido y solo del lado iluminado.
        _RimSharpness ("Rim Sharpness (cel)", Range(0, 1)) = 0
        _RimCut ("Rim Cut", Range(0.3, 0.95)) = 0.62
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowTint;
            float _Bands;
            float _Softness;
            float _RimStrength;
            float _RimSharpness;
            float _RimCut;
        CBUFFER_END

        // Normales de cara a _CameraNormalsTexture (contorno por aristas de OutlineRendererFeature).
        struct NormalsAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct NormalsVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        NormalsVaryings NormalsVert(NormalsAttributes input)
        {
            NormalsVaryings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }

        half4 NormalsFrag(NormalsVaryings input) : SV_Target
        {
            float3 n = normalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
            float2 octNormalWS = PackNormalOctQuadEncode(n);
            float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
            return half4(PackFloat2To888(remapped), 0.0);
            #else
            return half4(n, 0.0);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _GRASS_BURN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BurntGround.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
                float fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _BaseColor;
                output.fog = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light light = GetMainLight(shadowCoord);

                // Luz en bandas suaves (look pintado), multiplicada por la sombra.
                float ndl = saturate(dot(n, light.direction));
                float x = ndl * _Bands;
                float band = floor(x) + smoothstep(0.5 - _Softness, 0.5 + _Softness, frac(x));
                float lit = (band / _Bands) * light.shadowAttenuation;

                // Ambiente: cielo arriba, suelo abajo (SH de URP).
                half3 ambient = SampleSH(n);
                half3 albedo = input.color.rgb;
                half3 emission = 0;
                #if defined(_GRASS_BURN)
                emission = ApplyBurntGround(input.positionWS, albedo);
                #endif
                half3 shade = albedo * ambient * _ShadowTint.rgb;
                half3 col = lerp(shade, albedo * light.color, saturate(lit));

                // Luces adicionales (velas, faroles, magia): difusas, sin bandas.
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light extra = GetAdditionalLight(lightIndex, input.positionWS);
                    col += albedo * extra.color * extra.distanceAttenuation * saturate(dot(n, extra.direction) * 0.8 + 0.2);
                LIGHT_LOOP_END
                #endif

                // Borde iluminado suave para separar siluetas.
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = 1 - saturate(dot(n, view));
                float softRim = pow(fresnel, 4);
                float celRim = smoothstep(_RimCut - 0.03, _RimCut + 0.03, fresnel) * (0.25 + 0.75 * saturate(lit));
                float rim = lerp(softRim, celRim, _RimSharpness) * _RimStrength;
                col += rim * light.color + emission;

                col = MixFog(col, input.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Sombras y profundidad: los pases de URP Lit (no usan propiedades del material).
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }

        // En Deferred los materiales solo-forward escriben normales con este LightMode.
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
