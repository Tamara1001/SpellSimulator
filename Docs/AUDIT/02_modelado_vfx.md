# Auditoría de Solo Lectura: Modelado y VFX

> **Fecha:** 2026-11-22  
> **Área:** Modelado 3D (interiores, exteriores, jugador, manos VR, herramientas, props) y VFX (shaders, partículas, efectos mágicos, iluminación).  
> **Contexto:** Unity 6 (6000.3.14f1) + URP, PC y Meta Quest (OpenXR). Modo Libre (Pradera, Torre, Archivo, Laboratorio).

---

## 1. Resumen Ejecutivo y Alcance

Se realizó una auditoría técnica exhaustiva de código, shaders, geometría procedural, rigs e iluminación sobre el área de **MODELADO 3D** y **VFX/SHADERS** del proyecto *SpellSimulator*. Se revisaron los documentos de estado (`Docs/STATUS.md`, `Docs/KNOWN_ISSUES.md`, `Docs/MAPA_FLUJO_PLAN.md`), el registro de capturas en `Docs/Capturas/*.png` y los componentes en `Assets/_Project/`.

Se identificaron **14 hallazgos** (7 de Modelado y 7 de VFX) clasificados por severidad y tipo, con recomendaciones y alternativas concretas de solución acordes a las restricciones técnicas de PC y Meta Quest.

---

## 2. Hallazgos: Modelado (MOD)

### MOD-01: Avatar de red y jugador representados con primitivas crudas sin estilo cartoon
- **Severidad:** Media
- **Tipo:** Mejora / Falta
- **Archivo:Línea:** `Assets/_Project/Editor/NetSetup.cs:42-46`, `Assets/_Project/Scripts/Net/NetPlayer.cs:20-21`
- **Descripción:** El avatar multijugador (`NetPlayer.prefab`) se construye ensamblando primitivas crudas de Unity (cápsula para torso, esfera para cabeza, cubo alargado para la nariz y esferas para manos) utilizando el material `NetAvatar.mat` con shader `Universal Render Pipeline/Lit`. En modo individual (PC y VR), el jugador carece por completo de modelo en primera persona (no hay piernas, torso ni brazos visibles al mirar hacia abajo). El avatar desentona notablemente con la estética low-poly estilizada de *Witch Hat Atelier* que tienen los edificios, árboles y props del valle generados con `LowPolyMesh` y sombreados con `StylizedLit`.
- **Alternativas de arreglo:**
  1. Diseñar un modelo low-poly estilizado de aprendiz de mago (túnica con pliegues, sombrero puntiagudo y manos de 4 dedos) exportado en `.fbx` o generado proceduralmente con `LowPolyMesh`, asignando el shader `SpellSimulator/StylizedLit`.
  2. Sustituir las primitivas de `NetSetup.cs` por volúmenes cónicos y facetados que simulen una túnica de hechicero y capucha, asignando `StylizedLit` con soporte de tintado de color por jugador (`_BaseColor`).
  3. Añadir una malla en primera persona simplificada (mangas/brazos) atada a los puntos de agarre (`HoldPoints`) para dar presencia corporal al jugador en modo PC y VR.

---

### MOD-02: Manos en Realidad Virtual representadas como mandos de plástico Quest
- **Severidad:** Media
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/VrHandVisual.cs:15-35`, `Assets/_Project/Scripts/Player/PlayerInteractor.Vr.cs:45-47`
- **Descripción:** En modo VR, `VrHandVisual` muestra el modelo del controlador físico de Meta Quest (del sample importado de XRI). Al sostener una herramienta o lienzo, el mando simplemente se desactiva mediante `SetActive(false)`, dejando los objetos flotando en el aire sin ninguna representación de agarre manual. Ver controles plásticos contemporáneos rompe la inmersión de un simulador de hechicería clásica.
- **Alternativas de arreglo:**
  1. Integrar un par de manos/guantes estilizados de tela o cuero low-poly con un blend tree básico de poses (mano abierta en reposo, dedo índice extendido al apuntar/dibujar, puño cerrado al sujetar con grip).
  2. Reemplazar la referencia `model` en `VrHandVisual` por una malla low-poly estática de mano enguantada que sostenga naturalmente las herramientas.
  3. Si no se incorporan manos articuladas, modelar un soporte mágico (brazalete o manopla rúnica de piedra/bronce) que justifique la levitación de herramientas en el espacio VR.

---

### MOD-03: Libreta circular con hoja visual de disco pero colisión y trazo cuadrados
- **Severidad:** Media
- **Tipo:** Bug / Inconsistencia
- **Archivo:Línea:** `Assets/_Project/Scripts/World/CanvasFactory.cs:46-82`, `Assets/_Project/Editor/Map/CanvasDesigns.cs:81-120`
- **Descripción:** En la libreta circular pequeña (S), `CanvasFactory.DiscMesh()` genera una malla procedural de disco para las hojas. Sin embargo, el collider de dibujo físico y la proyección UV en `DrawingCanvas` provienen de la plantilla cuadrada `PaperCanvas`. El usuario puede dibujar y depositar tinta en las cuatro esquinas invisibles fuera del disco. Además, los lienzos instanciados directamente por `CanvasDesigns.cs` en la escena inicial no pasan por `CanvasFactory`, conservando hojas cuadradas (KI-144).
- **Alternativas de arreglo:**
  1. Reemplazar el `BoxCollider` en libretas circulares por un `CapsuleCollider` delgado o cilindro plano, y en `DrawingCanvas.GetUVFromWorldPosition` descartar puntos con `(uv - Vector2.one * 0.5f).sqrMagnitude > 0.25f`.
  2. Enmascarar la `RenderTexture` en el trazado de GPU (`DrawingCanvas.DrawAt`) mediante un shader circular que descarte fragmentos fuera del radio isotrópico.
  3. Actualizar `CanvasDesigns.cs` para invocar `CanvasFactory.Spawn` en todas las libretas circulares del mapa garantizando consistencia.

---

### MOD-04: Collider milimétrico y apiñamiento de herramientas en lapiceros del Archivo
- **Severidad:** Media
- **Tipo:** Bug / Usabilidad
- **Archivo:Línea:** `Assets/_Project/Editor/Map/ToolModels.cs:96, 114, 133, 154`, `Assets/_Project/Editor/Map/MapRestocker.cs:220-245`
- **Descripción:** Las herramientas (`Pencil`, `Brush`, `FlatBrush`, `BigBrush`, `Pen`) tienen un `CapsuleCollider` con un radio de entre 1,1 cm y 1,9 cm (`radius = 0.011f - 0.019f`). En el puesto del Archivo (`MapRestocker.PenCup`), se instancian de 3 a 5 herramientas en cada lapicero dentro de un cilindro de radio 9 a 18 cm. Apuntar a un collider de 1 cm con el rayo de interacción o con la mano en VR resulta errático y propenso a seleccionar la herramienta vecina. Adicionalmente, la reposición de `DisplayStock` puede expulsar piezas si hay solapamiento.
- **Alternativas de arreglo:**
  1. Configurar un `CapsuleCollider` agrandado con `isTrigger = true` específico para detección de interacción (`InteractionSetup.FixInteractMask`), manteniendo el collider físico fino solo para rebotes.
  2. Aumentar el ángulo de inclinación (`tilt`) en `MapRestocker.PenCup` de 7° a 18° para que los mangos queden en abanico abierto con mayor separación en la parte superior.
  3. Implementar un asistente de proximidad en `PlayerInteractor.Vr.cs` que priorice el objeto más cercano al eje del rayo en lugar del primer impacto raycast estricto.

---

### MOD-05: Fractura de objetos rígidos mediante división en cubos no adaptativa
- **Severidad:** Baja
- **Tipo:** Mejora
- **Archivo:Línea:** `Assets/_Project/Scripts/World/MeshBreaker.cs:30-85`, `Docs/KNOWN_ISSUES.md:72`
- **Descripción:** Cuando un objeto destructible recibe impacto de tierra o hechizos de fuerza (`MeshBreaker`), la subdivisión geométrica genera trozos perfectamente cúbicos en cuadrícula ortogonal (`KI-043`). Aunque en cajas rectangulares funciona visualmente, en props redondeados (barriles, estacas de piedra, pilares de la torre y rocas) la rotura resulta artificial y desentona con las mallas orgánicas low-poly.
- **Alternativas de arreglo:**
  1. Aplicar desplazamiento de ruido sobre los vértices de corte generados en `MeshBreaker.cs` para que las aristas de los fragmentos adopten facetas quebradas irregulares.
  2. Implementar prefabs pre-fracturados con cortes angulares para los props redondeados más comunes (barriles, ánforas, pedestales).
  3. Rotar aleatoriamente los planos de división de los fragmentos en lugar de seguir los ejes cardinales X, Y, Z.

---

### MOD-06: Estructuras y vigas flotantes al destruir paredes de la Torre
- **Severidad:** Baja
- **Tipo:** Inconsistencia
- **Archivo:Línea:** `Assets/_Project/Editor/Map/TowerBuilder.cs:98-100`, `Assets/_Project/Scripts/World/Breakable.cs:12-40`
- **Descripción:** Los segmentos cilíndricos del muro de la torre (`TowerWall`) tienen el componente `Breakable` para permitir que el hechizo *Wall Breaker* abra brechas en la piedra. No obstante, las molduras exteriores (`TowerTrim`), los marcos de ventanas y las vigas interiores de madera (`TowerBeams`) son GameObjects separados e indestructibles. Al destruir una sección de pared, las vigas y repisas permanecen flotando en el aire (KI-123).
- **Alternativas de arreglo:**
  1. Identificar en `TowerBuilder` los sectores del muro que contienen soportes estructurales o vigas y marcarlos con `Indestructible`.
  2. Enlazar los GameObjects hijos de vigas/molduras al evento de fractura del segmento de muro correspondiente para que caigan o se desintegren cuando el muro cede.
  3. Limitar la capacidad destructiva de *Wall Breaker* en la torre a paneles de madera y muretes específicos del tutorial, protegiendo el anillo perimetral portante.

---

### MOD-07: Ángulo de suelta y reposo de lienzos flotantes en VR
- **Severidad:** Baja
- **Tipo:** Mejora / Ergonomía
- **Archivo:Línea:** `Assets/_Project/Scripts/World/PhysicalCanvas.cs:60-110`, `Assets/_Project/Scripts/Player/PlayerInteractor.Vr.cs:194-206`
- **Descripción:** Al soltar un lienzo con la mano quieta en VR (`speed <= vrPinMaxSpeed`), el objeto queda flotando en el aire (`startFloating = true`). Si la mano tenía una torsión o inclinación accidental, la hoja queda en un ángulo incómodo para trazar con la pluma, obligando a agarrarla y acomodarla repetidamente.
- **Alternativas de arreglo:**
  1. Al ejecutar la acción de dejar flotando (`Release(pin: true)`), alinear automáticamente el lienzo para que su normal local quede orientada hacia la cámara del visor (con eje vertical paralelo a la gravedad).
  2. Añadir un botón rápido de "Enderezar lienzo" accesible desde el puntero VR o menú contextual.
  3. Aplicar un leve resorte angular (`KeepUpright`) que suavice la inclinación y estabilice el lienzo a 0°, 15° o 90° respecto a la horizontal.

---

## 3. Hallazgos: VFX y Shaders (VFX)

### VFX-01: Shader Binarize en CGPROGRAM legacy sin soporte URP ni estéreo VR
- **Severidad:** Alta
- **Tipo:** Bug / Compatibilidad
- **Archivo:Línea:** `Assets/_Project/Materials/Binarize.shader:14-58`
- **Descripción:** `Binarize.shader` está implementado con la sintaxis obsoleta de Built-in RP (`CGPROGRAM`, `#include "UnityCG.cginc"`, `fixed4`, `UnityObjectToClipPos`). No incluye las macros de Universal Render Pipeline (`Core.hlsl`), ni soporte para SRP Batcher, ni declaraciones de renderizado estéreo instanciado (`UNITY_VERTEX_OUTPUT_STEREO`). Si este shader se compila en builds WebGL o se ejecuta en Meta Quest, provoca errores de shader, caídas a fallback rosado o artefactos en un solo ojo.
- **Alternativas de arreglo:**
  1. Migrar `Binarize.shader` a HLSL nativo de URP utilizando `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"`, `TransformObjectToHClip` y macros de instanciamiento estéreo.
  2. Si el reconocimiento actual (`SentisGlyphClassifier` y `TensorRasterizer`) ya rasteriza los trazos vectoriales directamente en CPU/GPU sin utilizar este material, eliminar el shader y su asset para evitar deuda técnica.
  3. Convertirlo en un Fullscreen Blit Shader compatible con `OutlineRendererFeature` / `Blitter`.

---

### VFX-02: Saturación de partículas y sobrecosto de fill-rate en Meta Quest
- **Severidad:** Alta
- **Tipo:** Mejora / Rendimiento
- **Archivo:Línea:** `Assets/_Project/Scripts/Output/SpellManifestation.cs:100-185`, `Assets/_Project/Scripts/Output/ElementLook.cs:27-28`, `Assets/_Project/Scripts/Output/ElementVfx.cs:57-59`
- **Descripción:** Cada hechizo activo en 3D instancia entre 3 y 8 pistas de partículas Shuriken simultáneas (carga, ráfaga, chorro a 420 part/s, abanico a 360 part/s, vórtice a 400 part/s, más capas de polvo y estelas Trails). Con el límite configurado de 6 hechizos activos (`SpellOutputController.maxActive = 6`), el sistema puede superar las 6.000–9.000 partículas transparentes con colisión física activa en el mundo (KI-038). En GPU móvil (Meta Quest), este volumen de sobregiro (overdraw) supera el presupuesto térmico provocando caídas de framerate por debajo de 72 FPS.
- **Alternativas de arreglo:**
  1. Reducir dinámicamente las tasas de emisión (`rateOverTime` y `burstCount`) aplicando un factor de 0.35x cuando `PlatformDetector.IsVR` esté activo.
  2. Disminuir `maxActive` en VR de 6 a 2 o 3 hechizos concurrentes, y desactivar colisiones dinámicas de partículas en Quest (`collision.enabled = false` en `ElementVfx.cs`).
  3. Emplear `GraphicsQuality.ParticleScale` para modular automáticamente el límite máximo de partículas por pista según el perfil de calidad activo (`PC_RPAsset` vs `Mobile_RPAsset`).

---

### VFX-03: Suelo quemado (_GRASS_BURN) en StylizedLit con proyección planar XZ deformada en muros
- **Severidad:** Media
- **Tipo:** Inconsistencia / Falta
- **Archivo:Línea:** `Assets/_Project/Materials/StylizedLit.shader:86, 142-144`, `Assets/_Project/Materials/BurntGround.hlsl:1-40`
- **Descripción:** La keyword global `_GRASS_BURN` en `StylizedLit` calcula la quemadura del incendio de pasto proyectando las coordenadas mundiales en el plano XZ (`input.positionWS.xz`). Aunque en el terreno llano luce limpio y orgánico, al propagarse el fuego a cajas apiladas, muebles o muros de piedra verticales de la torre, la proyección planar se estira infinitamente en el eje vertical Y, generando bandas verticales negras deformadas.
- **Alternativas de arreglo:**
  1. Implementar mezcla triplanar en `BurntGround.hlsl` calculando la atenuación del fuego según la normal del objeto (`input.normalWS`), proyectando en XY/YZ para superficies con inclinación vertical pronunciada.
  2. Restringir la keyword `_GRASS_BURN` exclusivamente al material del suelo (`Terrain`), utilizando MaterialPropertyBlocks de tinte oscurecido local para objetos y props rígidos.
  3. Utilizar el sistema de calcomanías existente (`InscribedDecal` / `GroundMark.shader`) orientado a la superficie de los objetos en vez de un cálculo global por coordenadas XZ.

---

### VFX-04: Multiplicación de Luces Puntuales dinámicas sin hornear en interiores y combate
- **Severidad:** Media
- **Tipo:** Rendimiento / Glitch
- **Archivo:Línea:** `Assets/_Project/Editor/Map/TowerBuilder.cs:663-716`, `Assets/_Project/Scripts/Output/SpellManifestation.cs:186-199`
- **Descripción:** La torre genera 3 luces puntuales de iluminación general (`LibraryWarm`, `StudyWarm`, `AlchemyWarm`) más faroles con luces dinámicas (`PointLight` con rango 5-8.5 m en cada piso y entrada). Simultáneamente, cada hechizo activo (`SpellManifestation`) instancia una `Light` puntual de rango `s * 6f` e intensidad hasta `2.6x` con Empower. En URP Forward para móviles, el límite por objeto suele ser de 4 a 8 luces adicionales por píxel. La acumulación de luces en la biblioteca genera corte abrupto de sombras y luces ("popping") y sobrecosto de sombreado en Quest.
- **Alternativas de arreglo:**
  1. Hornear la iluminación estática de la torre (Lightmap offline para lámparas fijas), dejando los faroles con material emisivo HDR y bloom (`LanternGlow`) sin luz puntual en tiempo real.
  2. Limitar en `SpellOutputController` la emisión de luz dinâmica a un único hechizo (el más reciente), apagando el componente `Light` de las manifestaciones previas.
  3. Desactivar las luces puntuales de faroles secundarios en la variante móvil (`Mobile_RPAsset` con `AdditionalLightsMode.PerVertex` o límite reducido a 2 luces).

---

### VFX-05: Bucle estático de 16 ondas en StylizedWater sin culling de distancia
- **Severidad:** Baja
- **Tipo:** Rendimiento
- **Archivo:Línea:** `Assets/_Project/Materials/StylizedWater.shader:35, 98-111`, `Assets/_Project/Scripts/Reactions/WaterVolume.cs:80-130`
- **Descripción:** `StylizedWater.shader` ejecuta en el fragment shader un bucle fijo que itera hasta 16 ondas de impacto analíticas (`MAX_RIPPLES = 16`). En cada píxel del lago o estanque calcula distancias, exponenciales gaussianas y cosenos sin discriminar la distancia a la cámara. Píxeles lejanos del lago a 50 metros calculan micro-ondas que no tienen impacto perceptible en pantalla.
- **Alternativas de arreglo:**
  1. En `WaterVolume.cs`, filtrar y enviar a la GPU únicamente las ondas dentro del campo visual y radio de 20 metros alrededor del jugador.
  2. Salir inmediatamente del cálculo en el shader si `_RippleCount <= 0` o si la distancia al origen de la onda excede el radio de vida.
  3. Reducir `MAX_RIPPLES` a 6 en plataformas móviles manteniendo 16 para PC.

---

### VFX-06: Pie de cascada con terminación UV estática sin salpicadura dinámica en el lago
- **Severidad:** Baja
- **Tipo:** Calidad Visual
- **Archivo:Línea:** `Assets/_Project/Materials/Waterfall.shader:79-85`, `Assets/_Project/Editor/Map/MapDetails.cs:52`
- **Descripción:** La cascada del lago utiliza un ribbon transparente con vetas de ruido procedimentales. La espuma en la base se produce rígidamente en función de la coordenada UV vertical (`smoothstep(0.82, 1.0, uv.y)`). Si el oleaje o la malla del terreno presentan pequeñas desviaciones, el pie de la cascada parece una lámina sumergida sin generar turbulencia, salpicaduras ni transición de espuma orgánica en el punto de encuentro con el agua del lago.
- **Alternativas de arreglo:**
  1. Añadir un emisor de partículas secundario de impacto (`WaterEffects`) al pie de la cascada que libere gotas, vapor y espuma low-poly continua.
  2. Emplear la textura de profundidad `_CameraDepthTexture` en `Waterfall.shader` para generar espuma de intersección suave donde la cinta cruce la superficie de `StylizedWater`.
  3. Ajustar el desvanecimiento alfa en el borde inferior (`uv.y > 0.95`) para que la cinta se diluya dentro de la profundidad del agua.

---

### VFX-07: Viñeta y post-procesado no adaptados a visores VR en Profile global
- **Severidad:** Media
- **Tipo:** Confort VR / Rendimiento
- **Archivo:Línea:** `Assets/_Project/Editor/Map/MapDetails.cs:448-475`, `Docs/KNOWN_ISSUES.md:83` [KI-049]
- **Descripción:** `MapDetails.PostProcess()` genera un asset global `PostProcess.asset` que incluye Bloom, Tonemapping, ColorAdjustments y Vignette (`intensity = 0.2f`). La viñeta estática en pantalla fija resulta molesta y genera disonancia en VR (reduce el FOV periférico del visor), mientras que el Bloom y la viñeta añaden sobrecosto en GPU móvil que no está calibrado en Quest.
- **Alternativas de arreglo:**
  1. Desactivar en runtime el componente `Vignette` cuando `PlatformDetector.IsVR` sea verdadero.
  2. Crear dos perfiles separados: `PostProcess_PC.asset` (con Bloom y ajustes completos) y `PostProcess_Mobile.asset` (solo Tonemapping neutro y color adjustments sin viñeta ni bloom).
  3. Conectar los toggles de Bloom y Postproceso al sistema de calidad de `GraphicsQuality.cs` para permitir su desactivación en dispositivos de bajo rendimiento.

---

## 4. Matriz de Hallazgos y Prioridades

| ID | Área | Tipo | Severidad | Archivo Principal | Síntesis del Problema |
|---|---|---|---|---|---|
| **VFX-01** | VFX | Bug / Compat. | 🔴 Alta | `Binarize.shader:14` | Shader en CGPROGRAM legacy sin soporte URP ni estéreo VR. |
| **VFX-02** | VFX | Mejora / Perf. | 🔴 Alta | `SpellManifestation.cs:100` | Exceso de partículas y colisiones activas simultáneas en Quest. |
| **MOD-01** | Modelado | Mejora / Falta | 🟠 Media | `NetSetup.cs:42` | Avatar de red y jugador representados con primitivas crudas. |
| **MOD-02** | Modelado | Falta | 🟠 Media | `VrHandVisual.cs:15` | Manos en VR usan modelo plástico de mandos Quest sin guantes ni poses. |
| **MOD-03** | Modelado | Bug / Incons. | 🟠 Media | `CanvasFactory.cs:46` | Libreta circular con hoja visual de disco pero colisión cuadrada. |
| **MOD-04** | Modelado | Bug / Usab. | 🟠 Media | `ToolModels.cs:96` | Colliders de herramientas de 1 cm apiñados en los lapiceros del Archivo. |
| **VFX-03** | VFX | Inconsistencia | 🟠 Media | `StylizedLit.shader:142` | Suelo quemado (_GRASS_BURN) estirado en paredes y objetos verticales. |
| **VFX-04** | VFX | Rendimiento | 🟠 Media | `TowerBuilder.cs:663` | Saturación de luces puntuales dinámicas en interiores y hechizos. |
| **VFX-07** | VFX | Confort VR | 🟠 Media | `MapDetails.cs:448` | Viñeta y postproceso fijos no calibrados para visores VR (Quest). |
| **MOD-05** | Modelado | Mejora | 🟢 Baja | `MeshBreaker.cs:30` | Fractura por grilla cúbica en objetos redondeados u orgánicos. |
| **MOD-06** | Modelado | Inconsistencia | 🟢 Baja | `TowerBuilder.cs:98` | Vigas y molduras quedan flotando al destruir paredes de la Torre. |
| **MOD-07** | Modelado | Ergonomía | 🟢 Baja | `PlayerInteractor.Vr.cs:194` | Lienzos flotantes en VR pueden soltarse con inclinación incómoda. |
| **VFX-05** | VFX | Rendimiento | 🟢 Baja | `StylizedWater.shader:98` | Bucle estático de 16 ondas sin culling de distancia en fragment shader. |
| **VFX-06** | VFX | Calidad Visual | 🟢 Baja | `Waterfall.shader:79` | Pie de cascada plano sin salpicaduras 3D al nivel del lago. |

---

## 5. Conclusión y Pasos Sugeridos

1. **Prioridad 1 (Compatibilidad y Estabilidad Quest):**
   - Migrar o remover `Binarize.shader` (VFX-01).
   - Aplicar escala dinámica de partículas y reducción de luces en VR (VFX-02, VFX-04, VFX-07).
2. **Prioridad 2 (Usabilidad e Inmersión VR/PC):**
   - Ensanchar el cono de agarre y colliders de herramientas en los lapiceros (MOD-04).
   - Corregir el collider y trazado de la libreta circular (MOD-03).
   - Reemplazar los controles Quest por manos low-poly estilizadas (MOD-02).
3. **Prioridad 3 (Pulido Visual):**
   - Sustituir el avatar multijugador por un aprendiz de mago estilizado (MOD-01).
   - Adaptar la quemadura de fuego a superficies verticales o restringirla al terreno (VFX-03).
   - Agregar salpicaduras al pie de la cascada y refinar fracturas (VFX-06, MOD-05).
