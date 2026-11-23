# Brief 04: shader Binarize a HLSL/URP con soporte estéreo VR (VFX-01)

Proyecto: SpellSimulator (Unity 6 URP, target PC y Meta Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás `Assets/_Project/Materials/Binarize.shader`. **Nunca** `Binarize.mat`, `.meta` ni otros `.mat`/`.asset`. No podés compilar: sé conservador y comentá lo que cambiás.

## Contexto (informe `Docs/AUDIT/02_modelado_vfx.md`, VFX-01)
`Binarize.shader` está escrito en `CGPROGRAM` (Built-in legacy) sin soporte URP ni render estéreo (single-pass instanced) de VR. En Quest puede verse mal o romperse. Usá como referencia de estilo un shader URP del propio proyecto (por ejemplo `StylizedLit` o `StylizedWater`: buscalos en `Assets/_Project/` con grep) y cómo declaran el SubShader URP.

## Hacé
1. Leé el shader actual y el material (`Binarize.mat`, solo lectura) para listar las **propiedades** usadas y el **nombre del shader** (`Shader "..."`).
2. Reescribilo en **HLSL URP** (`HLSLPROGRAM`, `Core.hlsl` de `Packages/com.unity.render-pipelines.universal`), con `Tags { "RenderPipeline"="UniversalPipeline" }`, y estéreo: `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` y, si samplea la textura de cámara/pantalla, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` y los macros de textura en array.
3. **Conservá exactamente** el nombre del shader y los nombres, tipos y valores por defecto de las propiedades, para que `Binarize.mat` siga funcionando sin editarlo.
4. Mantené el comportamiento visual (binarizar por umbral). No agregues features. Evitá `discard` innecesarios y ramas costosas (target Quest).
5. Si el shader original usa algo que URP no soporta (GrabPass, por ejemplo), explicalo en la respuesta y proponé la alternativa (`_CameraOpaqueTexture`) sin implementar cambios de pipeline settings.

## Devolvé (≤5 líneas)
Qué cambió, propiedades conservadas, y cualquier cosa que requiera activar algo en los settings de URP (para que lo haga Tamara o yo por Unity MCP).
