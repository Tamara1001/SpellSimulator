# Laboratorio v2 — simulador de sellos (plan aprobado)

Reemplaza la mesa de perillas −/+ de [LABORATORIO_PLAN.md](LABORATORIO_PLAN.md). Aprobado por Tamara.

## Decisiones de Tamara
- Zona: pradera sureste, ~(47, −27). Sitio nuevo al final de los sitios de `MapStructures` ("Laboratory"); se borra `LabArea` (35,5, −16,5). `MapOverrides.json` no tiene entradas `Map/Lab*`.
- Se eliminan los botones −/+ (`LabStepPlate`) y `LabDial`: los reemplaza `LabControl` (perilla giratoria, deslizador, selector con muescas), arrastrable con mouse en PC y con muñeca/mano en VR.
- Variables: **todas las que normalmente se dibujan**, sin dibujarlas, fáciles de replicar, entender y usar. Decisión: el banco arranca de una **receta base** (las recetas de `Data/Spells`) con todos los modificadores sueltos; la opción «Libre» parte de un sello vacío (sigilo + signos sueltos). Una fila de controles por grupo (Papel/Anillo, Sigilo, Signos, Tinta/Prolijidad), con colores y un botón «Lanzar». Semilla fija para repetir. El grosor de pluma entra como variable (afecta solo el trazo dibujado y se registra en el CSV; no cambia el hechizo).

## Variables del banco (LabSealSpec)
| Variable | Rango | Default (constante del código) |
|---|---|---|
| Receta base | las de `Data/Spells` + «libre» | gust |
| Sigilo (elemento) | 9 de `Dictionary/sigils` | el de la receta |
| Tamaño del papel | 0,35 / 0,45 / 0,55 / 1 / 2 m | 0,45 |
| Llenado del anillo | 0,3–0,95 del papel | verificar con HarnessSeals |
| Tamaño del sigilo | ×0,6–1,3 | 0,57 R (`SigilOfRadius`) |
| Tipo de signo | 12 de `Dictionary/signs` | el de la receta |
| Cantidad de signos | 0–4, repartidos parejos | el de la receta |
| Ángulo base | 0–315°, pasos de 45° | 0 |
| Hacia adentro / afuera | 0 / 180° (`TurnDeg`) | 0 |
| Largo del signo | ×0,5–1,5 | 0,29 R (`SignOfRadius`) |
| Asimetría (un signo más largo) | ×0,6–1,6 | 1 |
| Distancia del signo al centro | 0,6–0,85 R | 0,714 (`SignDistance`) |
| Hueco del anillo | 0–40° | 0 (≥ 34° = `RingGapDeg`: queda «preparado») |
| Prolijidad (temblor) | 0–0,05 R | 0 |
| Tinta | Normal / Poderosa / Resistente | Normal |
| Cara del papel / inclinación | ±1 / 0–90° | arriba / 0° |
| Grosor de pluma | los 4 de la pluma | actual |

Entorno (se conservan): masa de la caja, viento (dirección, velocidad), fuego (semilla, debilitamiento, generaciones). Se eliminan caudal de agua y multiplicador de fuerza (los cubren la receta de agua y la tinta poderosa).

## Cómo funciona
`LabSealSpec` (struct puro) → `LabSealSynth.Build(spec, templateOf, rng)` devuelve trazos en UV (reutiliza `SpellStrokeBuilder`, `StampPattern.Template`, `GlyphEntry.TemplateStrokes`) → lienzo de prueba (`Session.Clear` + `AddStroke` + `Redraw`) → botón «Lanzar» → **`SpellCaster.Cast(canvas)`** (no `OnStrokeFinished`: sin auto-lanzamiento ni gasto de tinta). El ONNX, el compilador y `SpellIR` corren de verdad.

## Lag de las gafas (causa probable, sin medir)
Primer dibujo de TMP SDF / `Sprites/Default` / línea compila shaders y arma pipelines Vulkan en el frame de activación; hasta 32 `ForceMeshUpdate` de golpe en `LabOverlay.Refresh` (`LabOverlay.cs` ~576-595); caracteres `°`, `×`, tildes caen a la fuente de respaldo dinámica. Fix: precalentar en `LabOverlay.Start` repartido en varios frames (texto con todos los caracteres, `ForceMeshUpdate`, visible un frame casi invisible, apagar), `ShaderVariantCollection`/`GraphicsStateCollection` precalentada, fuente de atlas estático, máx. 8 etiquetas nuevas por refresco.

## Interacción
- Interfaz nueva `IManipulable` (`BeginManipulation(hit)`, `UpdateManipulation(in ManipulationInput)`, `EndManipulation()`, texto de ayuda) en `InteractionInterfaces.cs`.
- `PlayerInteractor.Manipulate.cs` (parcial nuevo): si lo apuntado es `IManipulable`, clic/gatillo (y grip en VR) lo captura y marca `clickConsumedByPressable`.
- PC: arrastre con mouse (`LookValue`) con `PlayerLook` congelado (precedente: `objectMover.IsRotating`); rueda sube/baja un paso (con guard para que no cambie grosor ni distancia).
- VR: giro de muñeca mueve perillas; desplazamiento de mano proyectado sobre el riel mueve deslizadores; funciona a distancia.
- `LabControlMath` puro: delta→valor, desenrollado del ángulo, snap a muesca, cruce de muesca. En cada cruce: `Haptics` + clic de audio. `SetText` solo cuando cambia.

## Viento
`LabWindVisual` reutiliza `ElementVfx` y el `ElementLook` del viento: estelas rizadas (`trails` + noise), hojas/pétalos girando, polvo a ras de piso > 10 m/s, chevrones en el piso, manga y banderines que se orientan y tensan con la velocidad (10 Hz, sin allocations). `WindVisualMath.Params(speed)` puro. Topes Quest: ≤ 150 partículas, ≤ 40 estelas. `Simulate` previo para que no arranque vacío.

## Área (estilo low-poly cartoon)
Plataforma de lajas r ≈ 5 m con toldo a rayas (paleta `Cloth`/`Plaster`/`Brass`); banco en U de 4,5 × 1,2 m (atril con papel al centro; ala izquierda controles del sello por color; ala derecha entorno + materializar); repisa de lentes y pizarrón de contadores; pista de pasto 12 × 10 m con estacas cada 2 m; manga de viento; fogón a ≥ 8 m; faroles, frascos, guirnaldas. Controles de 8–10 cm separados ≥ 6 cm (rayo VR).

## Riesgos
1. El ONNX puede no reconocer trazos sintéticos: smoke exige que cada receta con temblor 0 compile válida y con su elemento.
2. El lienzo de prueba es un `DrawingCanvas` real: excluirlo de agarrar, tinta, `recordSeals` y red; apuntar el disparo a la pista.
3. Conflictos de entrada (vista congelada, rueda, grip VR vs agarrar, clic consumido).
4. Quest: ~16 lecturas TMP + overlay; texto fijo horneado, solo se actualiza lo que cambia.
5. CSV: columnas nuevas al final (no romper `LabCsvTests` ni registros viejos).
6. El sitio puede chocar con la torre de vigía o pinos: `Find` con `taken`, `MapLab` despeja, verificar con captura.

## Tandas
| Tanda | Archivos | Depende de |
|---|---|---|
| T1 lógica pura | `Lab/LabSealSpec.cs`, `LabSealSynth.cs`, `LabControlMath.cs`, `WindVisualMath.cs`, `LabVariableDef.cs` (recortar catálogo), 3 tests en `Tests/EditMode` | — |
| T4 lag gafas | `Lab/LabOverlay.cs`, `Rendering/ShaderWarmup.cs` (nuevo) | — |
| T5 viento | `Lab/LabWindVisual.cs` (nuevo), `Lab/LabWindZone.cs` | firmas de `WindVisualMath` |
| T2 interacción | `Interfaces/InteractionInterfaces.cs`, `Player/PlayerInteractor.cs`, `PlayerInteractor.Manipulate.cs`, `PlayerLook.cs`, `ObjectMover.cs`, `Lab/LabControl.cs` (borra `LabDial`, `LabStepPlate`) | T1 |
| T3 banco | `Lab/LabSealBench.cs`, `LabParameters.cs`, `LabSessionLog.cs`, `LabCsv.cs` | T1 |
| T6 área y verificación | `Editor/Map/MapLab.cs`, `MapStructures.cs`, `MapBuilder.cs`, `FlammableSetup.cs`, `HarnessPlaySmoke.cs`, docs | todas |

Orden: T1+T4+T5 en paralelo → compilar → T2+T3 en paralelo → compilar → T6. Los subagentes no compilan; compila el agente principal.
