# Laboratorio v3 — pasada de usabilidad, reubicación del entrenamiento y mapa de la torre (plan aprobado)

Sigue a [LABORATORIO_V2_PLAN.md](LABORATORIO_V2_PLAN.md). Feedback de Tamara tras probar el v2 (textos ilegibles, papel con una sola dirección, confuso de usar, el laboratorio choca con el entrenamiento base, falta un mapa en la torre).

## Decisiones de Tamara
- «La casita» = la casa que está **justo enfrente de la torre, tomando la puerta de la torre como referencia** (no asumir una cabaña: hay que determinar cuál queda delante de la puerta mirando hacia donde mira la puerta, y ubicar el entrenamiento base en su entorno, con despeje del laboratorio en ~(47, −27)).
- El mapa de la torre es un **cuadro en la pared** (no maqueta). Pin estático «Vos estás aquí»; sin pin en vivo.
- «Modo básico» **no** significa quitar variables: tienen que seguir estando TODAS (muchas) pero fáciles de entender. Se resuelve con orden, agrupación numerada, rótulos claros, resumen en vivo, ayuda y reset por grupo, no escondiendo controles.
- El preset «Pista → banco» (el sello viene hacia el banco) está bien.

## (a) Bug de texto
Causa: en `MapLab.Control()` rótulo y lectura van en y = 0.004, el mismo plano que la franja de color (`p.Box(... 0.002f ..., alto 0.004f)`); TMP transparente con ZTest LEqual parpadea (z-fighting). Títulos de grupo a 1 mm, textos del tablero a 2 mm. Secundarias: `AutoSize` baja el texto a ~8 mm; rótulos de 0.03 m con ajuste de línea («Adentro/afuera» en 2 líneas); filas de 5 controles a 0.22 m de paso; perilla de 5 cm tapa el rótulo; poco contraste sobre franja al 32 %.
Arreglo (`MapLab.cs` + clase pura `LabPanelLayout` con test):
1. Constantes `SurfaceTop` y `TextLift = 0.006`: texto ≥ 6 mm sobre lo más alto que tenga debajo (tablero ≥ 4 mm). Nada coplanar.
2. NO ZTest Always (se vería a través de manos y perillas). TMP SDF no tiene Offset: la solución es la altura.
3. Cada control: plaquita crema elevada 3 mm con el rótulo en oscuro + ventanita oscura con la lectura en claro.
4. Sin autosize. Rótulo 0.16 (~1,6 cm), lectura 0.20, título de grupo 0.22; ajuste de línea apagado, desborde con «…»; rótulos ≤ 10 caracteres.
5. Máx. 3–4 controles por fila (paso ≥ 0.30 m); más filas si hace falta, SIN quitar variables.
6. Inclinación del panel 25° → 35°.
7. `shadowCastingMode = Off` en todos los TMP.
8. Test EditMode sobre `LabPanelLayout` (`TextY(surfaceTop)` ≥ 5 mm sobre cada superficie).

## (b) Presets de posición del papel
Variable nueva `LabSealVarId.PaperPose = 18` (al final, sin renumerar); `spec.paperPose` en `LabSealSpec`; columna nueva al final del CSV. Lógica pura `LabSealBenchMath.PaperPose(preset, face, tiltDeg, trackStart)` → posición local en el marco del lab, dirección de emisión y vector «arriba». `ApplyPaper` la usa con `FaceRotation` y `SetInkSign`.
| # | Preset | Dónde | Emite hacia |
|---|---|---|---|
| 0 | Atril → pista | atril | la pista (hoy cara arriba) |
| 1 | Atril → jugador | atril | atrás (hoy cara abajo) |
| 2 | Atril → arriba | atril | arriba |
| 3 | Piso | acostado en la pista a TrackStart+2 | arriba |
| 4 | Pista → izquierda | de pie en la pista a TrackStart+3 | −X |
| 5 | Pista → derecha | de pie a TrackStart+3 | +X |
| 6 | Pista → banco | de pie a TrackStart+6 | −Z, hacia el banco |
| 7 | Pista → piso | boca abajo a 1,5 m de altura | abajo |
| 8 | Libre | atril | usa paperFace/paperTilt como hoy |
En 0–7 `paperFace` invierte la emisión y `paperTilt` se ignora. Los laterales van en la pista, no en el atril. La cara visible del papel siempre mira al banco. El atril muestra un **espejo** (quad con `canvas.GetRenderTexture()`) y el papel real una **flecha de emisión** de color. Tests: perpendicularidad y sentido por preset; cara −1 invierte la emisión.

## (c) Usabilidad (todas las variables siguen visibles)
1. Texto legible (a).
2. Pizarra «Vas a lanzar» sobre el atril con `LabSealSummary.Build(spec)` (pura, StringBuilder reutilizado, test): receta, elemento, signos (n × tipo, adentro/afuera), tinta, posición del papel, estado previsto («cerrado»/«preparado» si hueco ≥ 34°); muestra la calidad del último lanzamiento.
3. Resultado tras Lanzar: banda verde ✔ / roja ✖, elemento, calidad %, motivo (`spell.status`); destello en el papel y sonido.
4. Botón LANZAR grande, rojo, centrado delante del atril (0.35 × 0.25 m); «Sello por defecto» al costado, más chico.
5. Orden lógico izquierda→derecha / adelante→atrás con títulos numerados («1 Receta/Sigilo», «2 Signos», «3 Papel y posición», «4 Tinta/Prolijidad»); consola derecha = Entorno. Flujo: izquierda arma, centro ve y lanza, derecha mundo.
6. Tira de ayuda world-space por consola con `LabSealVarDef.Help` del control apuntado (la HUD UI Toolkit no se ve en VR, KI-095).
7. Rango: muescas de mín/máx y muesca dorada en el valor por defecto. Reset por grupo (↺ al final de cada fila; `LabBenchAction.ResetGroup` → `bench.ResetVars(ids)`).
8. Colores consistentes: el color del grupo se repite en perilla, franja, pizarra y lente.
9. VR: colliders ≥ 9 cm, resaltado al apuntar, texto ≥ 1,5 cm a 0,8 m, tira de ayuda a la vista del rayo.

## (d) Reubicación del entrenamiento
Conflicto medido: laboratorio en (47, −27) mirando (−0,97, 0,26); su pista termina cerca de (30, −22,5), a ~6 m de la mesa de estampas (30, −16,5), y lo lanzado sigue hacia el atril del campo (28, −14), los blancos (28, −10,4) y los muñecos (28, −5), (24,5, −11).
Propuesta del planificador: nuevo origen del entrenamiento al sur de la torre (atril ≈ (1, −24) emitiendo ≈ (0,12, −0,99)); **Tamara aclaró que la referencia es la casa justo enfrente de la puerta de la torre: determinar su posición real y usar su entorno**.
Mecánica sin romper nada:
- `ValleyShape.TrainingOrigin` y `TrainingForward` nuevos, con aplanado de radio 8 m (suave 5,6–8). `FieldCenter` NO cambia (lo usan torre, círculo de piedras, santuario, peñascos, taller de tierra, terreno y `CanPlantTree`).
- Pasto y árboles con `ClearVegetation`; no tocar `CanPlantTree` (cambiaría el azar de los pinos).
- Clase pura `TrainingLayout.ToWorld(localOffset)` con test; la usan `RelocateGameplay`, `BuildFieldProps`, `MapContraptions` y `CanvasDesigns` (papel gigante y caballete). `StampArea` y el KeepOut de `PracticeZones` apuntan al sitio nuevo.
- Distribución relativa al atril A (igual a la de hoy, rotada): blancos +4,4 m adelante; muñecos +3 y +9 m; mesa de herramientas, mesa de estampas y artilugios detrás de A a ±2,2 m; papel gigante y caballete a ~±6 m.
- Los PathSpots siguen usando el punto viejo del campo como destino de la ruta (`Practice/*_Path8/9` tienen overrides).
- `MapOverrides.json` guarda la pose de `GiantPaper_Field` (y una entrada `Canvas` sin identificar): descartar por código con `MapLayout.DropEntries(...)` una sola vez, con aviso en log; no editar el JSON a mano.
- Nombres sin cambios (`PaperCanvas`, `Dummy_*`, `TestProps`): harness y smoke siguen andando.
- Laboratorio se queda en (47, −27), mismo frente. `TrackClear` agrega zona prohibida alrededor de `TrainingOrigin` (r = 12 m); el campo viejo queda libre como fondo de tiro. Opcional: terraplén bajo al final de la pista (~(29, −22)). Anclas de `LabSpawner` sin cambios.

## (e) Mapa en la torre (cuadro en la pared)
Cuadro/plano pintado en una pared de la planta baja de la torre, visible desde el spawn, fuera del pasillo puerta-centro (x ±0,8). Generado por código de Editor; marcadores numerados con color por zona, marcador «Vos estás aquí» en la torre, leyenda con número, nombre y descripción corta (TMP ≥ 1,6 cm, elevado 4 mm sobre la superficie). Mouse y rayo VR: apuntar a un marcador muestra el nombre (`MapZoneMarker : IPressable`), presionar resalta la línea de la leyenda.
Una sola fuente de datos: `Data/WorldZones.json` (id, nombre, descripción, color, origen = sitio de `MapStructures` o constante de `ValleyShape`: Training, Tower, Lake, Spawn, WaterLab, Laboratory…). Posiciones resueltas al generar (`MapStructures.TryGetSite` + `TrainingOrigin`). Zonas: entrenamiento, laboratorio, santuario, círculo de piedras, laboratorio de agua, taller de tierra, cabañas, cueva, puesto, peñascos, torre de vigía, lago. Lógica pura `MapProjection.WorldToTable(world, center, scale)` con test (proyección a coordenadas de plano 2D).

## (f) Riesgos
1. El aplanado nuevo puede mover el `Find` del puesto o de la Cabaña 1 → test que fije posiciones actuales de sitios.
2. Overrides viejos reaplicados en el lugar anterior.
3. Lanzamientos desde la pista hacia el banco dañan al jugador o rompen consolas → revisar `Health` y `FlammableSetup` para `Lab*`.
4. `LabOverlay` y `LabSessionLog` suponen papel en el atril.
5. Compatibilidad CSV (columna nueva al final).
6. Cantidad de TMP en Quest: `SetText` solo cuando cambia algo.
7. Verificar con `HarnessCapture` la rampa y estantes.
8. `GrimoireTexts` y `GAME_DESIGN.md` dicen «campo de práctica al este»: actualizar.

## (g) Tandas
| Tanda | Modelo | Archivos | Depende de |
|---|---|---|---|
| T0 texto | Opus | `MapLab.cs`; `Scripts/Lab/LabPanelLayout.cs` (nueva) y su test | — |
| T1 lógica papel y resumen | Sonnet | `LabSealSpec.cs`, `LabVariableDef.cs`, `LabSealBenchMath.cs`, `LabSealBench.cs`, `LabSealSummary.cs` (nueva), `LabCsv.cs`, `LabSessionLog.cs`, tests | — |
| T3 entrenamiento | Sonnet | `ValleyShape.cs`, `TrainingLayout.cs` (nueva), `MapBuilder.RelocateGameplay`, `MovablesSetup.cs`, `MapContraptions.cs`, `CanvasDesigns*.cs`, `MapStructures.cs` (StampArea, TrackClear), `MapLayout.cs` (DropEntries), tests | — |
| T2 UI del banco | Opus | `MapLab.cs`, `LabBenchDisplay.cs` (nueva), `LabHelpStrip.cs` (nueva), `LabBenchButton.cs`, `LabControl.cs` | T0, T1 |
| T4 mapa de la torre | Opus | `Editor/Map/TowerMap.cs`, `Scripts/World/MapZoneMarker.cs`, `Scripts/World/Map/MapProjection.cs`, `Data/WorldZones.json`, test | T3 |
| T5 cierre | principal | línea en `MapBuilder.Build`, `HarnessPlaySmoke` (lanzamiento válido por preset, texto sobre superficies, objetos del entrenamiento dentro del área, marcadores = catálogo), docs (CHANGELOG, KNOWN_ISSUES, GAME_DESIGN, GrimoireTexts) | todas |
Orden: T0 → T1 y T3 en paralelo → compilar → T2 y T4 en paralelo → compilar → `MapBuilder.Build()` → T5 → capturas.
Los subagentes no compilan; compila el agente principal entre tandas.
