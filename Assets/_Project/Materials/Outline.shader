// Contorno entintado de pantalla (OutlineRendererFeature). Siluetas: laplaciano de la profundidad inversa (1/z es
// afin en pantalla para cualquier plano, asi que da cero en suelos rasantes y solo salta en las discontinuidades); la
// tinta cae del lado cercano, por eso el cielo nunca se entinta. Aristas vivas: normales de _CameraNormalsTexture
// (solo con _OUTLINE_NORMALS). Lineas mas finas y tenues con la distancia. Apto XR (Blit.hlsl + TEXTURE2D_X).
// Interruptor global: _OutlineStrength (0 apaga).
Shader "Hidden/SpellSimulator/Outline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Outline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local_fragment _ _OUTLINE_NORMALS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #if defined(_OUTLINE_NORMALS)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            half4 _InkColor;
            float4 _OutlineParams;   // x grosor (px), y umbral de silueta, z coseno minimo de normales, w fuerza de normales
            float4 _OutlineFade;     // x inicio fade (m), y fin fade (m), z opacidad minima, w afinado lejano
            float _OutlineIntensity;
            float _OutlineStrength;  // global

            float EyeDepth(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);

                float strength = _OutlineIntensity * _OutlineStrength;
                float dC = EyeDepth(uv);
                // Cielo / plano lejano: nunca se entinta.
                if (strength <= 0.0 || dC > _ProjectionParams.z * 0.9) return col;

                float far01 = saturate(dC / _OutlineFade.y);
                float thickness = _OutlineParams.x * lerp(1.0, _OutlineFade.w, far01);
                float2 px = thickness * (_ScaledScreenParams.zw - 1.0);

                float2 uvL = uv - float2(px.x, 0);
                float2 uvR = uv + float2(px.x, 0);
                float2 uvD = uv - float2(0, px.y);
                float2 uvU = uv + float2(0, px.y);
                float dL = EyeDepth(uvL);
                float dR = EyeDepth(uvR);
                float dD = EyeDepth(uvD);
                float dU = EyeDepth(uvU);

                // Laplaciano de 1/z normalizado: dC/dL + dC/dR - 2. Negativo = el centro esta delante (silueta, lado cercano).
                float lapX = dC / dL + dC / dR - 2.0;
                float lapY = dC / dD + dC / dU - 2.0;
                float th = _OutlineParams.y;
                float edge = smoothstep(th, th * 2.0, -min(lapX, lapY));

                #if defined(_OUTLINE_NORMALS)
                {
                    float3 nC = SampleSceneNormals(uv);
                    float3 nR = SampleSceneNormals(uvR);
                    float3 nU = SampleSceneNormals(uvU);
                    // Solo entre superficies a profundidad parecida (las siluetas ya las cubre la profundidad).
                    float sameR = step(abs(dR - dC), dC * 0.05);
                    float sameU = step(abs(dU - dC), dC * 0.05);
                    float crease = max((1.0 - smoothstep(_OutlineParams.z - 0.1, _OutlineParams.z, dot(nC, nR))) * sameR,
                                       (1.0 - smoothstep(_OutlineParams.z - 0.1, _OutlineParams.z, dot(nC, nU))) * sameU);
                    // Las aristas internas solo de cerca: de lejos cada mata de pasto se volvia una mota de tinta.
                    crease *= 1.0 - smoothstep(_OutlineFade.x * 0.3, _OutlineFade.x * 0.7, dC);
                    edge = max(edge, crease * _OutlineParams.w);
                }
                #endif

                float fade = lerp(1.0, _OutlineFade.z, smoothstep(_OutlineFade.x, _OutlineFade.y, dC));
                float a = saturate(edge * fade * strength * _InkColor.a);
                col.rgb = lerp(col.rgb, _InkColor.rgb, a);
                return col;
            }
            ENDHLSL
        }
    }
}
