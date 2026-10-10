---
tags: [doc, harness]
---
# Harness — cómo verificar cambios

El Editor de Unity suele estar **abierto** mientras se trabaja, así que no se usa `-batchmode`
(Unity no permite dos instancias sobre el mismo proyecto). La verificación se hace contra el Editor vivo
vía **Unity MCP** (`com.unity.ai.assistant`).

## Checklist de verificación (en orden)

1. **Compilación**: después de editar `.cs`, forzar import y leer consola.
   - MCP `Unity_RunCommand` con `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)`.
   - `Unity_ManageEditor GetState` → esperar `IsCompiling: false`.
   - `Unity_ReadConsole Types=[Error,Warning]` → **0 errores**. Warnings nuevos: justificar o arreglar.
2. **Tests EditMode** (lógica pura):
   - Menú `SpellSimulator > Harness > Run EditMode Tests`, o por MCP:
     `SpellSimulator.EditorTools.HarnessTestRunner.RunEditMode();`
   - Resultado en `Logs/harness-editmode.txt` (primera línea `PASS`/`FAIL passed=N failed=M …`)
     y en consola con prefijo `[Harness] EditMode:`. Es asíncrono: leer el archivo unos segundos después.
3. **Chequeos de proyecto**: `SpellSimulator > Harness > Check Project Settings` (URP asignado, Color Space Linear).
4. **Validación de escena**: `SpellSimulator > Harness > Validate Open Scene` o
   `SpellSimulator.EditorTools.HarnessSceneValidator.Validate()` (devuelve el reporte).
   Chequea singletons del pipeline (`SentisGlyphClassifier`, `SpellCaster`, `SpellOutputController`, `PlatformDetector`)
   y referencias serializadas nulas en componentes `SpellSimulator.*`.
5. **Play Mode** (si el cambio es de runtime): `Unity_ManageEditor Play`, ejercitar, leer logs
   (`[Hechizo]`, `[Cast]`, `[Reconocimiento]`, `[VFX Output]`, `[PlatformDetector]`…), `Stop`. Captura opcional con `Unity_Camera_Capture`.
6. **VR**: no se puede verificar desde el agente. Dejar en el resumen qué probar en el Quest.

### Smoke test de cast (Play Mode)
Con `Unity_ManageEditor Play`, un `Unity_RunCommand` puede dibujar un sello sintético en el lienzo real y castear:
`canvas.Clear()`, agregar a `canvas.Session` un círculo (r 0.42 UV, con o sin hueco) + `dictionary.Get("fire").TemplateStrokes`
escalados a 0.22 en el centro, `canvas.Redraw()`, `caster.Cast(canvas)` y loguear `valid/active/element/primaryManifestation`.
Resultado esperado (2026-10-07): fuego cerrado → activo; agua con hueco de 45° → preparado; tierra + column abajo → `column`.

## Mover / renombrar scripts (importante)
El Editor **auto-refresca** mientras se editan archivos desde afuera: si un `.cs` desaparece un instante, Unity borra su
`.meta` huérfana y el GUID se pierde (los componentes en escenas/prefabs quedan "Missing").
- Antes de una reestructura: `Unity_RunCommand` con `AssetDatabase.DisallowAutoRefresh();` y al terminar `AllowAutoRefresh()` + `Refresh`.
- Para conservar un componente al renombrar la clase: mover la `.meta` junto con el `.cs` (mismo GUID) y mantener los
  nombres de campos serializados que importan.
- Si se perdió un GUID: `git show HEAD:<ruta>.meta > <nueva ruta>.meta` antes del próximo refresh.
- `AssetDatabase.MoveAsset` falla por MCP con assets `.vfx` (pide interacción): mover en disco con la `.meta`.

## Datos
- `SpellSimulator > Dictionary > Sync From Reference`: copia JSON del diccionario y el modelo desde `wha-spell-simulator`
  (ruta en EditorPrefs, default `../../../wha-spell-simulator` relativo a `Assets/`) y regenera `GlyphDictionary.asset`.
  Por código: `GlyphDictionarySync.Sync(path, copyModel)`.
- `SpellSimulator > Dictionary > Rebuild Asset`: sólo regenera el asset (tras editar JSON a mano).

## Assemblies

| asmdef | Ruta | Contenido |
|---|---|---|
| `SpellSimulator.Runtime` | `Assets/_Project/Scripts/` | Todo el código de juego. |
| `SpellSimulator.Editor` | `Assets/_Project/Editor/` | Herramientas del harness (menú `SpellSimulator/…`). |
| `SpellSimulator.Tests.EditMode` | `Assets/_Project/Tests/EditMode/` | Tests NUnit de lógica pura. |

Al agregar un `using` de un paquete nuevo en runtime, sumar su asmdef a `references` de `SpellSimulator.Runtime.asmdef`.

## Build de Windows
`Unity_RunCommand` con `BuildPipeline.BuildPlayer` (escenas `Pradera.unity` y `Escena.unity`, en ese orden; `StandaloneWindows64`, salida `Build/SpellSimulator.exe`,
ignorada por git). Verificar arrancándola con `-logFile` unos segundos: debe loguear `[PlatformDetector] Modo: PC` y 0
excepciones fuera de `[XR]` (OpenXR sin casco loguea `XR_ERROR_FORM_FACTOR_UNAVAILABLE`: esperado). Los ~500 warnings
de la build son shaders del paquete de inferencia.

## Bots de red
Jugadores automaticos para probar la red sin gente (F1). Son el ejecutable de Windows del juego lanzado con `-bot` (`Scripts/Net/Bots/NetBot.cs`, opciones en `BotOptions`):
se unen por codigo de Relay (`-botjoin CODIGO`) o por IP directa (`-botdirect ip:puerto`), entran al juego, caminan y saltan con un gamepad virtual, escriben un informe
(`-botlog`) y salen con codigo 0 (bien) o 1 (mal). Cada bot usa su propio perfil de autenticacion (varios ejecutables en la misma PC compartirian el inicio de sesion anonimo).
Sin ventana el cursor no se captura y el jugador no rota con la vista: el bot gira el cuerpo directamente.

- **Build:** menu `SpellSimulator/Build/Windows (bots)` -> `Builds/Bot/SpellSimulator.exe` (233 MB; la primera vez cambia la plataforma activa del editor; despues 30 s-2 min). Rehacerlo cuando cambia el codigo de juego o de red. Resultado en `Logs/build-bot.txt`.
- **Pruebas** (menu `SpellSimulator/Harness/...`, resultado en `Logs/harness-bots.txt`, informes de cada bot en `Logs/bots/`): `Run Bots (local)` (el editor hospeda por loopback y lanza 3 bots 35 s), `Run Bots (Relay)` (igual, por Relay real; necesita Internet), `Run Bots (churn local)` (los bots entran y salen 3 veces). El anfitrion comprueba cuantos bots ve a la vez, que se mueven (>= 4 m horizontales) y que sus avatares desaparecen al irse.
- **Limitaciones actuales:** solo caminan y saltan; agarrar objetos y lanzar hechizos se suman en F2/F3. Con Unity en segundo plano `delayCall` no corre: los builds usan `EditorApplication.update`.

## Menús de Interaction y Map
`SpellSimulator > Interaction > Setup Current Scene` deja movibles y rayables las cosas del mundo. `Map > Build Pradera`
reconstruye todo el mapa (incluye herramientas con modelo nuevo, mesas de la torre, lienzos y contraptions) y llama a ese setup.

## Errores de compilación que no aparecen
Si una clase nueva no aparece (`Type.GetType` da null) y la consola por MCP no muestra errores, buscar `error CS` en
`%LOCALAPPDATA%/Unity/Editor/Editor.log`: los errores de un asmdef (p. ej. el de Editor sin referencia a
`Unity.TextMeshPro`) frenan toda la recarga y a veces no llegan a la consola.

## Mapa (M5)
`SpellSimulator > Map > Build Pradera` (o `MapBuilder.Build()` por MCP) regenera el mapa en `Pradera.unity`: borra el
objeto `Map` y lo vuelve a crear, guarda las mallas en `Art/Generated` y reubica jugador, papeles, herramientas,
blancos y muñecos. Determinista. Para cambiar el mapa: tocar `ValleyShape` (forma) o `TowerBuilder` (torre) y regenerar.

## Aprendizaje
`SpellSimulator > Learning > Setup Current Scene` (o `LearningSetup.SetupCurrentScene()`) agrega `Discovery`, `Grimoire`
e `InteractionPromptUI`. El progreso se guarda en `persistentDataPath/descubrimientos.json` (lo comparten editor y
build): borrarlo después de probar para no arrancar con descubrimientos de prueba.

## Audio
`python Tools/Audio/synth_audio.py` regenera los WAV sintetizados (`Audio/Synth`). `SpellSimulator > Audio > Setup
Current Scene` (o `AudioSetup.SetupCurrentScene()` por MCP) rehace `Data/AudioLibrary.asset` y equipa la escena abierta
(correrlo en Pradera y en Escena). Regenerar el mapa conserva el audio (vive en SystemManagers y Player).

## Sellos de plantilla (Play Mode)
`SpellSimulator.EditorTools.HarnessSeals.Cast("PaperCanvas", "fire", ("column", 270f), ("column", 90f))` dibuja sigilo,
signos (id, ángulo en el anillo; orientados hacia adentro) y anillo con las plantillas del diccionario, y lanza. Sirve
para probar reacciones: fuego → blancos ardiendo; agua → apagado; tierra → fractura; `MagicWorld.ResetWorld()`.

## Captura visual
`SpellSimulator.EditorTools.HarnessCapture.Capture(ruta, posición, objetivo)` renderiza a PNG con una cámara temporal (la
cámara del jugador usa Cinemachine y `Unity_Camera_Capture` falla). Para ver un hechizo en pleno cuerpo:
`caster.Cast(canvas)` → `manifestation.FastForward(1.4f)` → `Capture(...)`. Las capturas van al scratchpad, no al repo.

**Galería de primitivas** (Play Mode): sigilo + signos con `TemplateStrokes` rotados (`rot = (bearing − 270) + twist`;
twist 0 = adentro, 180 = afuera, −90 = antihorario). Casos de referencia: fuego + 2 columnas adentro = chorro;
agua + 4 afuera = abanico; viento + molinete = vórtice; fuego + 2 levitaciones = orbe; agua + 2 pulls = solo succión;
luz + 1 columna a la izquierda = chorro lateral.

**Física en tiempo real**: guardar posiciones (cultura invariante: `Vector3.ToString` usa coma decimal), lanzar,
`sleep 4` en Bash y medir. Los blancos están en `TestProps` (plataforma a la altura del chorro).

## Tests (131 al 2026-10-07)
| Archivo | Cubre |
|---|---|
| `ParsingTests` | RingDetector (cerrado/abierto/dos arcos/ajuste), StrokeGrouper (transitividad), SymbolCandidate, capas, StrokeCleaner, GeometryUtils |
| `RecognitionTests` | Softmax, TTA, TensorRasterizer, GlyphDictionary, **end-to-end modelo: 20/20 plantillas** |
| `SealActivationTests` | Fases del sello: preparado, cerrar ⇒ lanzar, gastado no relanza, inválido reintenta, borrar rearma |
| `UserCaseTests` | **End-to-end con el modelo (CPU)**: sigilos grandes en anillo chico (5 elementos), sigilo + 2 columnas, sigilo solo no es anillo |
| `InkTests` / `RagToolTests` | Depósito de tinta y trapo |
| `PoseTests` | La pose recupera la rotación (column 0/45/90/180/270°, pull, levitation) |
| `PlanTests` | Leyes R‑05 a R‑21: chorro vertical, inclinación por signo largo, abanico, vórtice, potencia sin dirección, orbe, levitación invertida, solo pull, lente, spin-up, fuga |
| `SealFixtureTests` | **Sellos reales grabados** (`Tests/Fixtures/*.json`) reproducidos con el modelo (hoy: `fire_columnas_bien`, `fire_columnas_mal`); ida y vuelta del formato; anotaciones sobre el papel |
| `ReactionTests` | Fuego (enciende, se sostiene, se consume, rompe), agua (apaga, moja, bloquea ignición, apaga aun bajo fuego), viento (aviva/enfría), tierra (rompe madera, no piedra), secado, tabla por material |
| `DamageTests` | Daño de hechizo en rango de referencia, agua/viento casi no dañan, quemadura 4/s, impacto con umbral y tope 25 |
| `ValleyShapeTests` | Mapa: torre en meseta plana, lago bajo el agua, montañas cierran el valle, pendiente caminable, sin árboles en spawn/campo/torre |
| `AudioTests` | Todos los cues que usa el código existen y tienen clips; pasos según superficie |
| `LearningTests` | Textos para todos los glifos del diccionario, todo glifo se puede aprender, progreso ida y vuelta |
| `StampTests` | Con el modelo: el sigilo estampado deja el anillo abierto (preparado), cerrado a mano lanza su elemento, columnas estampadas forman un chorro |
| `SplitJoinTests` | Fusion de las mitades de un sello partido (UV proporcional al ancho) y reglas de "pegadas lado a lado" |
| `CompilerTests` | Gates (sin anillo, baja confianza, sin sigilo, varios sigilos), activo/preparado, levitation, dirección inward, cancelación, fuerza, escala |

## Sellos grabados (fixtures de pruebas manuales)
`SpellCaster` (con `recordSeals` activo) guarda cada sello lanzado en `persistentDataPath/Sellos/sello_<fecha>.json`
(build de Windows: `%USERPROFILE%\AppData\LocalLow\<Company>\SpellSimulator\Sellos`; la ruta exacta sale en el log `[Cast] Sello guardado`).
Contiene los trazos crudos en UV, la escala isotrópica del lienzo y el resumen del panel. Para convertirlo en test:
copiarlo a `Assets/_Project/Tests/Fixtures/` y agregar `"expect": { "sigil": "fire", "primitives": "Burst, Jet" }`.
`SealFixtureTests` loguea `[Fixture]` con la lectura de cada signo aunque no haya `expect`.

## Escribir tests
- Lógica pura → EditMode, sin escena. El pipeline sin red se prueba fabricando `GlyphPrediction` (ver `CompilerTests.Fake`).
- Fábricas en `TestStrokes`: `Circle`, `Arc`, `Line`, `FromTemplate`, `Session`.
- `TestAssets.Dictionary` carga el diccionario real (requiere haber corrido el sync).
- Nombre: `<Clase>Tests`, método `Condicion_ResultadoEsperado`.
- PlayMode tests: todavía no hay asmdef; crear `Tests/PlayMode/` cuando haga falta (ej. el smoke test de cast con `SentisGlyphClassifier` real).

## Graphify
Grafo de conocimiento del código (AST, sin LLM) para navegar dependencias y la vista de Obsidian.

**Instalación en Windows:** `pip install graphifyy` (con doble «y»). El comando `graphify` puede no estar en el PATH: usar `python -m graphify ...`.
`python -m graphify update Assets/_Project` escribe `Assets/_Project/graphify-out`: **moverlo a la raíz** (`./graphify-out`, ignorado por git) para no generar `.meta` dentro de `Assets/`.
Leer `graphify-out/GRAPH_REPORT.md` antes de explorar código ahorra muchos tokens.

```bash
graphify update .                           # re-extrae (incremental) → graphify-out/graph.json
graphify export obsidian --dir Docs/Graph   # notas + canvas para Obsidian
graphify query "¿cómo llega un trazo al SpellCompiler?"
graphify path "PhysicalTool" "SpellOutputController"
graphify god-nodes                          # hubs arquitectónicos
```
- `.graphifyignore` limita el grafo a `Assets/_Project/` y `Docs/` (excluye paquetes, samples, Library).
- `graphify-out/` y `Docs/Graph/` están en `.gitignore`: se regeneran localmente.
- Hay hooks git (`graphify hook install`, locales en `.git/hooks/`) que **re-generan `graph.json` en cada commit/checkout**.
  La vista de Obsidian no se regenera sola: correr el `export obsidian` después.
- Los nombres de comunidades se escribieron a mano en `graphify-out/.graphify_labels.json` (el auto-labeling por LLM
  no tiene backend configurado). Si un re-cluster los vuelve `Community N`, re-etiquetar y re-exportar con
  `--labels graphify-out/.graphify_labels.json`.
- Colores por comunidad en la Graph view: `Docs/.obsidian/graph.json` (grupos por `tag:#community/...`).

## Trucos del MCP de Unity (lo que cuesta redescubrir)
- **Parámetros de `Unity_RunCommand`:** `Code` y `Title` (con mayúscula; antes eran `code`/`title`). El código es
  `internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) {...} }`. Prohibido: reflexión con
  `BindingFlags`, `System.Diagnostics.Stopwatch`; usar `Type.GetType("Ns.Clase, Ensamblado")` y `Time.realtimeSinceStartup`.
  Ensamblados: `SpellSimulator.Runtime` y `SpellSimulator.Editor`.
- **Compilar:** `AssetDatabase.Refresh()` y esperar ~40 s (loop `for i in $(seq 1 14); do sleep 3; done`, un `sleep` suelto largo está
  bloqueado). Errores: `tail -n 3000 "$LOCALAPPDATA/Unity/Editor/Editor.log" | grep "error CS"` (la consola por MCP a veces no los muestra).
  Un error de un asmdef frena toda la recarga.
- **Editar scripts durante Play** provoca recarga y rompe estado privado: salir de Play antes (`Unity_ManageEditor` Action `Stop`).
- **Reconstruir el mapa:** `SpellSimulator.EditorTools.Map.MapBuilder.Build()` (~15 s; guarda la escena y llama a los setups de interacción,
  inflamables y UI). Todo lo de escena sale de ahí: nada se edita a mano.
- **Build de jugador:** `BuildPipeline.BuildPlayer` falla con "inside the player loop" si se llama directo; usar `EditorApplication.delayCall`
  y leer `[Build...] Succeeded` en Editor.log. Smoke test del exe: `-screen-width 640 -screen-height 360 -logFile Logs/smoke.txt`
  (NO `-nographics`: la calibración del modelo necesita GPU) y buscar "Modo: PC" y "Pose calibrada: True".
- **Tests EditMode:** `HarnessTestRunner.RunEditMode()`; si `Logs/harness-editmode.txt` queda en RUNNING (la recarga lo cortó) relanzar.
- **Test de humo de Play:** `HarnessPlaySmoke.Run()` y leer `Logs/harness-playsmoke.txt` (PASS/FAIL, ~20 s). Cubre inventario de dos manos,
  cuaderno, dibujo, reinicio del mundo, fuego, grimorio, HUD y errores de consola. **Agregar un chequeo por cada sistema nuevo.**
- **Entrada real:** `SpellSimulator/Harness/Run Real Input Probe` (`HarnessRealInput`): teclado y mouse virtuales del Input System agarran con E, Tab, dibujan con el mouse y sueltan en 6 lienzos; resultado en `Logs/harness-realinput.txt`. Util para bugs de la cadena completa (interactor + vista completa + puntero + lapiz). No lanzar dos pruebas a la vez.
- **Matriz de lienzos:** `SpellSimulator/Harness/Run Canvas Matrix` y leer `Logs/harness-canvasmatrix.txt` (~7 min). Casos en `TESTING.md`.
- **Capturas:** `HarnessCapture.Capture(ruta, pos, mira, w, h, fov)` (no ve `Graphics.DrawMeshInstanced` ni UI Toolkit); para ver el juego real
  `ScreenCapture.CaptureScreenshot(ruta)` en Play y leer el PNG 1-2 s después.
- **Harness de interacción:** `PlayerInteractor.GrabItem(go)`, `SelectHotbar(i)`, `ObjectMover.End(pin)`, `NotebookPages.Turn(n)`, `Grimoire.Open()`,
  `GrimoireButton.Press()`, `GrassFire.Ignite(pos, radio)`, `MagicWorld.ResetWorld()`, `Hud.Notify(id, texto, seg)`.
- **Archivos:** los `.cs` del proyecto usan CRLF (ver `.gitattributes`); si una edición falla por "no se encuentra el texto", casi siempre es
  un final de línea. Los scripts Python largos se escriben con la herramienta Write (los heredocs con `\n` o comillas se rompen); los patrones
  multilínea de `Edit` deben reproducir los finales de línea reales.
- **Varios agentes con Unity:** un candado `mkdir Logs/unity.lock` / `rmdir` (si existe, esperar). Los agentes escriben código y yo compilo y
  verifico; delegar solo tareas de 1-2 archivos con la especificación cerrada.
- **Ajustes manuales de la escena:** `MapBuilder.Build()` regenera todo, pero `MapLayout` conserva lo que muevas a mano (ver CHANGELOG 2026-10-08). Para fijar tu layout sin esperar un Build: menu `SpellSimulator/Map/Capturar mi layout manual`. `Borrar overrides del layout` vuelve al layout generado.
