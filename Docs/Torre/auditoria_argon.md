# Auditoría Integral de Diseño de Niveles, Estética, Código y VR — Torre-Tutorial (`Argon`)

> **Alcance:** Auditoría técnica, visual, pedagógica y de implementación sobre `Docs/Torre/` (`00_resumen.md`, `carteles.md`, `01_terraza.svg`, `02_piso2.svg`, `03_piso1.svg`, `04_planta_baja.svg`), contrastada contra `Docs/Torre/auditoria_codex.md`, `Docs/GAME_DESIGN.md`, `Docs/MAPA_FLUJO_PLAN.md` (§17–§18) y el código real de generación y gameplay en `Assets/_Project/` (`TowerBuilder.cs`, `MapTutorial.cs`, `TowerProps.cs`, `MapDetails.cs`, `CanvasDesigns.cs`, `TutorialPaperGuide.cs`, `PhysicalCanvas.cs`, `PlayerInputReader.cs`, `PlayerMovement.cs`, `PlayerInteractor.cs`).

---

## 1. Veredicto por Piso y Diagnóstico Ejecutivo

| Nivel / Archivo | Veredicto | Motivo principal |
|---|---|---|
| **Nivel 1 · Terraza** (`01_terraza.svg`) | **RECHAZADO** | **75 `<text>`** y 9 capas gráficas superpuestas; 3 colisiones directas de etiquetas en el dibujo; el Cartel #3 («Mantené C») activa el estado *tumbado* (`Prone`) en el código real a los `0,35 s` e impide saltar el muro siguiente; el Cartel #5 repite controles en vez de indicar solo estado; falta cerrar en código el círculo completo de la baranda interior (`r = 4,8 m`). |
| **Nivel 2 · Piso 2** (`02_piso2.svg`) | **CON CAMBIOS** | **70 `<text>`**; los 3 ejercicios opcionales (4 estaciones + 4 carteles + 4 flechas celestes + 3 cajas punteadas) dominan visualmente el centro del plano y eclipsan la ruta obligatoria; el Cartel #10 (`227, 536`) queda dibujado por fuera de la línea verde de avance (`245, 505`); el Cartel #8 en `carteles.md` mira hacia el Marco 1 en lugar de mirar al corredor de aproximación. |
| **Nivel 3 · Piso 1** (`03_piso1.svg`) | **CON CAMBIOS** | **64 `<text>`**; buena secuencia pedagógica en serie (`325° → 270° → 220° → 195°`), pero hay una **trampa mecánica crítica**: el hechizo se lanza en el instante en que se cierra el anillo (`SpellCaster.Cast`), mientras que el rayo guía (`TutorialPaperGuide.aim`) solo se enciende cuando el lienzo está agarrado (`grab.IsHeld`). Si el jugador calca y cierra el anillo sobre el atril antes de apretar `E` (como le ordena el Cartel #13), el hechizo se dispara al techo antes de agarrar el papel. Además, `CanvasDesigns.DraftingTable` (`217°, r = 2,0 m`) colisiona en código con el nuevo atril (`220°, r = 2,6 m`). |
| **Nivel 4 · Planta Baja** (`04_planta_baja.svg`) | **RECHAZADO** | **61 `<text>`**, con **15 textos apiñados en una franja sur de `230 px`** (`y = 475..705`); rotula explícitamente las 3 trabas y sus recetas (*Agua/Aire*, *Tierra + Crush*) violando la decisión de enseñar **una sola solución guiada** (viento al pestillo) y dejar agua y tierra sin cartel ni rótulo; el texto de `Note_Tutorial_Door` difiere en los 3 documentos y supera los 38 caracteres; en código siguen vivos `WallBoard_Library`, `BigCanvas_Library`, `ReadingTable`, `Stamp_M_5` y las velas de `LibraryDesk`. |

---

## 2. Verificación de `auditoria_codex.md`: Aciertos, Falsos Positivos y Correcciones

No se repite la prosa de Codex; a continuación se verifica punto por punto su auditoría, separando lo verdadero de sus **falsos positivos, errores geométricos y dudas ya cerradas por decisiones de Tamara**:

### 2.1 Matriz de verificación de los 20 puntos de Codex (§3.1–§3.20)

| Punto en `auditoria_codex.md` | Veredicto Argon | Verificación técnica contra el código y las decisiones de Tamara |
|---|---|---|
| **#1 Apertura del hueco (`48°..180°` vs `180°→0°`)** | ❌ **Falso positivo** | Codex confundió el hueco de la losa con el desarrollo total de la rampa. Con `RampTurn = 180°` y `Headroom = 3,0 m`, `TowerBuilder.FloorHole` corta la losa en `48°..180°` para la rampa que **baja**, mientras que sobre la losa maciza `48°→0°` aterriza el tramo final de la rampa que **viene de arriba**. Es 100 % exacto en 3D; solo debe simplificarse su representación gráfica 2D. |
| **#2 Dirección de rampa y llegada (`180°→90°→0°`)** | ⚠️ **Parcial** | Los textos y flechas coinciden en que se entra a bajar en `180°` (Oeste) y se desembarca en `0°` (Este). Lo que confunde en `02_piso2.svg` y `03_piso1.svg` es dibujar ambas rampas (la que llega de arriba y la que baja abajo) conectadas en `48°` como si fueran un continuo en el mismo nivel. |
| **#3 Piso 2: Cartel #10 (`216°`) vs. Pila (`198°`)** | ⚠️ **Falso positivo angular / Error real en SVG** | Angularmente `216°` **sí está `1,75 m` antes** de `198°` viniendo por el sur (`270° → 216° → 198° → 180°`). Lo que Codex no vio es que en `02_piso2.svg` la curva Bézier del camino pasa por `(245, 505)` (`r ≈ 4,6 m`), dejando al icono `⑩` `(227, 536)` afuera de la ruta dibujada. |
| **#4 Piso 2: Posiciones de ejercicios y Cartel #8** | ✅ **Cierto** | `00_resumen.md` dice `55°` y `105°` para los marcos 1 y 2; `carteles.md` y `02_piso2.svg` usan `60°` y `110°`. Además, `carteles.md` orienta el Cartel #8 hacia `(65°, r=2,8m)` («hacia el marco 1») con rumbo `15°`, quedando de costado para quien viene por el disco central. |
| **#5 Terraza: Orientación del Cartel #4 (`342°`)** | ❌ **Falso positivo** | En sentido horario (`25° → 2° → 342° → 325°`), el jugador que sale de la viga en `25°` camina por `2°` de frente al Cartel #4 en `342°` (`viewer = (2°, r=6,6m)` es perpendicular a la tangente de avance). La orientación es correcta. |
| **#6, #7, #8, #9 Desfasajes angulares en Carteles #11–#14** | ✅ **Cierto** | Hay desfases de `2°` a `10°` entre `00_resumen.md` y `carteles.md` (`#11`: `338°` vs `340°`; `#12`: `285°` vs `295°`; `#13`: `232°` vs `238°`; `#14`: `282°` vs `285°` y atril de puerta en `282°` vs `262°`, pestillo `258°` vs `256°`, contrapeso `282°` vs `284°`, piedra `294°` vs `296°`). |
| **#10 Salida y desbloqueo al cruzar el vano** | ❌ **Falso positivo** | Codex afirmó que cruzar el vano para apagar la barrera no figura en el diseño, pero **ya está implementado y testeado** en `TutorialRules.InDoorway(local)` y `TutorialProgress.Advance(local)` ([TutorialRules.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/World/Map/TutorialRules.cs#L40-L80)). |
| **#11 Libro «Los cuatro elementos» sin fuego en la torre** | ✅ **Cierto (ya decidido en D29)** | El libro `elements` enseña los 4 sigilos base (que ya están en `starting`), pero según `D29` el fuego se practica afuera porque el mobiliario de la torre es inflamable. |
| **#12 Cartel #5 (reja de la terraza)** | 🔒 **Cerrado por Tamara** | El cartel de la reja solo indica estado (`Reja cerrada / Completá agacharse y saltar`) y no repite controles. |
| **#13 y #14 Jerarquía de opcionales y Cartel #9 en Piso 2** | ✅ **Cierto** | Los 3 opcionales saturan el plano y la línea 2 del Cartel #9 tenía 37 caracteres visibles. Se corrige en las Secciones 6.2 y 7. |
| **#15 y #16 Dudas sobre controles `R` y `T` (#11 y #12)** | 🔒 **Cerrado por Tamara** | Controles confirmados: `R` recarga tinta y `T` abre el grimorio (corroborado además en `PlayerInputReader.cs` líneas 152 y 230). |
| **#17, #18 y #19 Uso de «sello», versiones de #14 y «guía»** | ✅ **Cierto** | No se enseñan sellos/estampas en la torre (se usa «viento» en el lienzo), y se unifica el texto del Cartel #14 en la Sección 7. |
| **#20 Rampa de Piso 1 y cortina en `195°`** | ⚠️ **Parcial** | En `03_piso1.svg` la cortina en `195°` sí cierra el paso entre la baranda (`r = 4,8 m`) y el muro (`r = 6,2 m`) justo antes de la boca de rampa (`180°`), pero las etiquetas y las sogas tensoras amontonadas en `(142..226, 456..491)` tapan el dibujo del bloqueo. |

---

## 3. Medición Real de Saturación y Colisiones en los 4 SVG

Todos los SVG usan `viewBox="0 0 1200 820"` (`984.000 px²`), divididos en un lienzo de planta (`800 × 820 px`, escala `40 px = 1 m`, centro en `400, 410`) y un panel lateral derecho (`380 × 780 px` en `x = 800..1180`, que consume el **31,7 % del ancho total**).

### 3.1 Tabla de saturación medida por archivo

| Métrica de saturación | `01_terraza.svg` | `02_piso2.svg` | `03_piso1.svg` | `04_planta_baja.svg` | Meta Plano de Jugador |
|---|---:|---:|---:|---:|---:|
| **Total de nodos `<text>`** | **75** | **70** | **64** | **61** | **12 – 18** |
| `<text>` sobre el dibujo de planta (`x < 800`) | 35 | 32 | 28 | 28 | **10 – 14** |
| `<text>` en el panel lateral (`x ≥ 800`) | 40 | 38 | 36 | 33 | **2 – 4** (solo título y leyenda mínima) |
| Caracteres visibles totales en el SVG | ~2.180 | ~2.140 | ~2.190 | ~1.990 | **≤ 280** |
| Tamaño de fuente en planta (`font-size`) | `10` – `13 px` | `9,5` – `13 px` | `10` – `12,5 px` | `9,5` – `12,5 px` | **`13` – `16 px`** |
| Capas gráficas simultáneas en planta | 9 capas | 10 capas | 9 capas | 9 capas | **4 capas** (estructura, ruta, estaciones, carteles) |
| Cajas verdes de «Zona libre» (`#58d68d`) | 4 arcos + 4 textos | 5 cajas + 6 textos | 3 cajas + 3 textos | 2 cajas + 2 textos | **0** (pertenecen a ficha técnica) |
| Líneas de cota celestes (`#85c1e9`) | 3 cotas + 3 textos | 2 cotas + 2 textos | 2 cotas + 2 textos | 1 cota + 1 texto | **0** (solo barra de escala exterior) |

### 3.2 Colisiones geométricas exactas detectadas en el dibujo (`x < 800`)

1. **[01_terraza.svg](file:///D:/Programas/Unity/SpellSimulator/Docs/Torre/01_terraza.svg):**
   - **Colisión 1 (Este, `y = 264..301`):** `Testigo C (22° · r=7,6m)` en `(694, 301)` atraviesa el arco exterior de `Zona Viga` (`r = 328 px`, que en `y = 301` pasa exactamente por `x = 709 px`) y queda a `22 px` debajo de `Luz libre y=1,30m` `(712, 279)`.
   - **Colisión 2 (Sureste, `y = 597..620`):** `Testigo Salto (322° · r=7,6m)` en `(630, 620)` con `text-anchor="end"` (`x ≈ 505..630`) se superpone con el trazo del muro bajo `(557.3, 520.1) → (668.7, 598.1)` y con el borde inferior de `Zona Muro`.
   - **Colisión 3 (Oeste, `y = 382..410`):** `Boca rampa (180° · y=13,5m)` en `(195, 382)` (`x ≈ 55..195`) pisa el borde del hueco `(154, 410) → (208, 410)` y el inicio de la flecha de bajada `(181, 395)`.
2. **[02_piso2.svg](file:///D:/Programas/Unity/SpellSimulator/Docs/Torre/02_piso2.svg):**
   - **Colisión 1 (Noroeste, `x = 242..300`):** El rótulo inclinado `Baranda hueco (r=4,8m · 48°..205°)` en `(242, 345) rotate(-65)` se mete dentro del rectángulo punteado `Zona Canto: 3,6 × 3,5 m` (`x = 260..404, y = 250..390`).
   - **Colisión 2 (Centro-Este, `x = 420..565, y = 340..390`):** `Mesa + Lienzo (20° · r=2,5m)` en `(494, 348)` y su subtítulo en `(494, 360)` quedan encerrados dentro de `Zona Flotar` (`x = 415..559, y = 255..403`) y son cruzados por la flecha opcional `M 488 360 Q 475 330 466 315`.
   - **Colisión 3 (Sureste, `x = 478..541, y = 491..518`):** El círculo del Cartel `#9` `(529, 491, r=12)`, la caja de tiro `(490, 500, 18×18)`, la etiqueta `Caja tiro` `(478, 514)` y el borde del polígono `Línea Tiro` se tocan en un radio de `25 px`.
3. **[03_piso1.svg](file:///D:/Programas/Unity/SpellSimulator/Docs/Torre/03_piso1.svg):**
   - **Colisión 1 (Sureste, `y = 493..544`):** `1 · Mesa Pluma + Tinta + Cinturón (325° · r=3,6m)` en `(538, 526)` (`x ≈ 410..666`) pisa la esquina inferior de la mesa rotada en `(518, 493)` y casi toca `Zona Tinta: 3,5 × 3,2 m` en `(538, 544)`.
   - **Colisión 2 (Suroeste, `x = 200..365, y = 445..544`):** En un cuadrado de `165 × 100 px` se cruzan el polígono de `Zona Práctica`, el atril `(320, 477)`, sus 2 líneas de texto `(295, 445..458)`, la flecha celeste `Guía de apunte → Viento` `(255, 490)`, la ruta verde, el Cartel `#13` `(339, 508)` y la línea de cota `Distancia limpia al blanco: 3,20 m` `(y = 528..544)`.
4. **[04_planta_baja.svg](file:///D:/Programas/Unity/SpellSimulator/Docs/Torre/04_planta_baja.svg):**
   - **Colisión masiva en la Plaza Sur (`y = 475..705`):** Se acumulan **15 nodos `<text>`** en `230 px` verticales. El título `Plaza Sur de Resolución (Viento / Agua / Tierra): 5,5 × 4,8 m` en `(400, 484)` (`x ≈ 240..560`) choca directamente con el icono del atril de `Los cuatro elementos` en `(480, 477)` y con la curva del camino en `(495, 475)`.

---

## 4. Lo que Codex NO Vio: Controles, Consistencia y Factibilidad en Unity

### 4.1 Tres problemas de controles entre los carteles, `GAME_DESIGN.md`, `GrimoireTexts.cs` y `PlayerInputReader.cs`

1. **BUG CRÍTICO DE DISEÑO/CÓDIGO — «Mantené C para agacharte» (Cartel #3) tumba al jugador en el piso (`Prone`) y bloquea el salto:**
   - En [PlayerInputReader.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Player/PlayerInputReader.cs#L333-L345), mantener presionada la tecla `C` durante `ProneHoldSeconds = 0.35f` dispara `IsTogglingProne = true`.
   - En [PlayerMovement.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Player/PlayerMovement.cs#L98-L111), `prone` es un **toggle permanente** (`if (!prone) prone = true; target = 2f;`): baja la cápsula a `0,65 m`, reduce la velocidad a `0,9 m/s` (arrastrarse) y **cuando el jugador suelta `C` tras cruzar la viga, sigue tumbado en el piso**.
   - Peor aún: en [PlayerMovement.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Player/PlayerMovement.cs#L224-L229), `if (prone) { if (inputReader.IsJumping && HasHeadroom(standHeight)) prone = false; return; }`. Cuando llega al muro de salto (`325°`) y aprieta `Espacio`, **no salta** (solo se levanta).
   - **Corrección obligatoria:** El Cartel #3 **no debe decir «Mantené C»**; debe decir `<b>C</b> · Agachate bajo la viga` (un toque de `C` o aclarar en código que en `PlayerInputReader` el mantener `C` no active `Prone` mientras el jugador se mueve o durante el tutorial).

2. **Trampa mecánica en Piso 1 (#13) y Planta Baja (#14) — Cerrar el anillo lanza el hechizo ANTES de agarrar el lienzo con `E`:**
   - Según [GAME_DESIGN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/GAME_DESIGN.md#L62-L66) y `SpellCaster.Cast`, **el hechizo se lanza automáticamente en el instante en que se cierra el anillo**.
   - Por otro lado, en [TutorialPaperGuide.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Lab/TutorialPaperGuide.cs#L105), la flecha celeste de apunte (`AimGuide`) **solo se muestra mientras el jugador sostiene el lienzo en la mano** (`bool show = grab != null && grab.IsHeld;`).
   - En `carteles.md`, el Cartel #13 ordena: *«Paso 3 · Calcá con Clic izq. el viento / Agarrá con E y apuntá la guía a la tela»*. Si el jugador calca la plantilla completa (cerrando el anillo) sobre el atril y recién después aprieta `E` para agarrarlo y apuntar, **el hechizo ya se disparó desde el atril antes de agarrarlo** y el sello quedó gastado.
   - **Solución técnica y de diseño:**
     - **En código (`TutorialPaperGuide.cs`):** Mostrar el `AimGuide` también cuando el lienzo está flotando (`grab.IsFloating`) o apoyado en el atril, **o bien** hacer que el jugador primero agarre/ubique el lienzo apuntando al objetivo (o que el atril inclinado a `25°` ya apunte su `EmissionNormal` directo a la cortina/pestillo, o que con `E` lo deje flotando frente a la cortina/pestillo y ahí dibuje/cierre el anillo).
     - **En los carteles #13 y #14:** Indicar el orden real que funciona en el motor: `Dibujá viento en el lienzo` / `<b>E</b> apuntalo a la cortina` (y en PB: `<b>E</b> apuntá al pestillo`), asegurando que el atril o el agarre con `E` orienten la cara con tinta hacia el blanco antes de cerrar el anillo.

3. **Desincronización de la fuente de verdad de controles (`Docs/GAME_DESIGN.md` vs. `GrimoireTexts.cs` vs. `PlayerInputReader.cs`):**
   - `CLAUDE.md` exige que `Docs/GAME_DESIGN.md`, `GrimoireTexts.cs` y `PlayerInputReader.cs` coincidan, pero hoy presentan 4 discrepancias con los controles confirmados por Tamara:
     - **Dejar flotando (`E`):** En [GAME_DESIGN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/GAME_DESIGN.md#L87-L101) el cuerpo del texto aún dice que se usa **`G`**, mientras que la tabla de línea 204 y [PlayerInteractor.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Player/PlayerInteractor.cs#L209) usan **`E` de nuevo** (y clic de rueda).
     - **Vista de lienzo (`Tab`) vs. Tinta del cinturón (`V`):** En [GAME_DESIGN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/GAME_DESIGN.md#L102) y [GrimoireTexts.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Learning/GrimoireTexts.cs#L73) dice que **`V`** fija el lienzo a la pantalla, pero en [PlayerInputReader.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/Player/PlayerInputReader.cs#L155-L215) **`Tab`** es `focusAction` (vista de lienzo) y **`V`** es `inkCycleAction` (cambiar clase de tinta del cinturón).
     - **Grimorio (`T`):** En [GAME_DESIGN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/GAME_DESIGN.md#L118) dice *"Tab lo abre"*, cuando la tecla real en `PlayerInputReader.cs` (línea 230) es **`T`** (`Esc` a pantalla completa).
     - **Tumbarse (`Z` vs. mantener `C`):** La tabla de `GAME_DESIGN.md` (línea 197) asigna `Z` a tumbarse, pero en `PlayerInputReader.cs` (línea 218) `Z` pasa a la página anterior (`PagePrev`).

### 4.2 Factibilidad constructiva en Unity (`TowerBuilder.cs`, `MapTutorial.cs`, `TowerProps.cs`, `MapDetails.cs`, `CanvasDesigns.cs`)

Al contrastar `00_resumen.md` §6 con el código C# actual, hay **6 roturas de escena** que ocurrirían al cambiar las constantes si no se corrigen estos métodos:

1. **Techo (`RoofMesh`) y vigas (`BeamsMesh`) rotos al subir `OuterRadius` a `6,8 m` y cambiar `RampTurn` a `180°`:**
   - En [TowerBuilder.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/TowerBuilder.cs#L315-L326), `RoofMesh()` tiene radios hardcodeados para la torre chica: el aro dorado mide `5,0..5,5 m` de radio y el alero baja de `8,1 m` a `4,6 m`. Si `PostRadius` sube a `6,50 m` y `TerraceOuter` a `8,70 m`, **los 10 postes (`r = 6,5 m`) quedan `1,0 m` por fuera del aro del techo (clavados en el vacío) y el alero (`8,1 m`) queda más angosto que el parapeto (`8,7 m`)**. `RoofMesh()` debe escalar su aro a `6,3..6,8 m` y su alero a `9,3 → 5,8 m`.
   - En [TowerBuilder.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/TowerBuilder.cs#L438-L450), `BeamsMesh()` calcula qué vigas llegan al muro asumiendo `RampTurn = 270°` (`float d = Mathf.Repeat(a + 60f, 360f); float outer = d < 110f ? InnerRadius - 0.05f : RampInner - 0.08f;`). Con `RampTurn = 180°`, las vigas del norte (`30°, 90°, 150°`) chocarán con la rampa si no se invierte la condición al hemisferio sur (`180°..360°`).
2. **Baranda circular de la Terraza (`r = 4,8 m`):**
   - En [MapTutorial.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/MapTutorial.cs#L267), `Rails(b, parent, "Tutorial_TerraceRailIn", TowerBuilder.RampInner, HoleFrom, HoleTo - 8f, y)` solo construye baranda en el arco del hueco (`48°..172°`). Si la viga y el muro pasan a ocupar solo el corredor `r = 4,8..8,2 m` (en vez de `ObstacleFrom = -3f`), **hay que cerrar la baranda interior de `0°` a `360°` en la terraza**, o el jugador esquivará ambos obstáculos caminando por el disco central.
   - Además, en [TerraceRules.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/World/Map/TerraceRules.cs#L20-L28), `OnLine` debe verificar `along >= 4.8f && along <= Radius` (solo el semieje positivo del ángulo, no `Mathf.Abs(along)` que detectaría también a `-BeamAngle + 180°`).
3. **Colisión de muebles viejos no listados en `00_resumen.md` (`CanvasDesigns.cs`, `MapDetails.cs`, `TowerProps.cs`):**
   - En **Piso 1**, [CanvasDesigns.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/CanvasDesigns.cs#L63-L64) instancia `DraftingTable` en `(-1.6, floor, -1.2)` (`ang = 216,8°, r = 2,0 m`) y `Easel_Study` en `(1.5, floor, -1.0)` (`ang = 326,3°, r = 1,8 m`), y [TowerProps.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/TowerProps.cs#L92-L93) instancia `Chest_Study_A` y `Crate_Study_A`. **`DraftingTable` queda a `0,6 m` del nuevo atril del lienzo guiado (`220°, r = 2,6 m`), tapando el paso.**
   - En **Planta Baja**, si se quita `LibraryDesk` como pide `00_resumen.md`, quedan **flotando en el aire** las 2 velas encendidas de [MapDetails.TowerAmbience](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/MapDetails.cs#L226) (`(-0.7, 0.82, 0.6)` y `(0.72, 0.82, 0.6)`) y la estampa `Stamp_M_5` de [MapDetails.Stamps](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/MapDetails.cs#L330) (que además viola la regla de «nada de sellos/estampas en la torre»). También deben quitarse o reubicarse `ReadingTable` ([TowerBuilder.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/TowerBuilder.cs#L538)), `Paper_S`/`Paper_M` ([MapDetails.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/MapDetails.cs#L407-L408)), `WallBoard_Library` y `BigCanvas_Library` ([CanvasDesigns.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/CanvasDesigns.cs#L53-L60)).
4. **Física del lienzo agarrable sobre un atril inclinado `25°`:**
   - En [PhysicalCanvas.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/World/PhysicalCanvas.cs#L38-L43), cuando un lienzo tiene `rb.isKinematic = false; rb.useGravity = true`, al tocar un collider inclinado resbala o vibra contra el tope. Para que `TutorialPaper_Wind` (Piso 1) y `TutorialPaper_Door` (Planta Baja) descansen inclinados `25°` sin caerse jamás al piso y sigan siendo agarrables con `E`, deben instanciarse con **`rb.isKinematic = true`** (igual que `GiantCanvas_Field` en [CanvasDesigns.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Editor/Map/CanvasDesigns.cs#L74-L75)): al agarrarlos con `E` (`OnInteract` / `OnRemoteGrab`), `PhysicalCanvas` toma el control normalmente, y `ResetToInitial()` restaura `initialKinematic = true`.
5. **Separación de mallas en la nueva «Cortina Pesada» (`TutorialCloth` vs. `Tutorial_ClothBeam`):**
   - `00_resumen.md` §4 propone barral de hierro con 7 argollas y 2 sogas laterales atadas a la baranda y al muro. En [TutorialCloth.cs](file:///D:/Programas/Unity/SpellSimulator/Assets/_Project/Scripts/World/TutorialCloth.cs#L80-L89), al abrirse (`Open()`), el objeto dinámico vuela y achica su `localScale` a `0`. Por ende, en `MapTutorial.MagicFloor()` la viga, el barral y las argollas deben ir en la malla estática `Tutorial_ClothBeam`, mientras que el paño plegado y las sogas tensoras deben ir en la malla dinámica `Tutorial_Cloth` (si las sogas se ponen en la malla estática, quedarán atravesadas en el aire tras volar la cortina).
6. **Consistencia con [MAPA_FLUJO_PLAN.md](file:///D:/Programas/Unity/SpellSimulator/Docs/MAPA_FLUJO_PLAN.md#L59) (§17–§18 y tracker `R4`):**
   - El paso `R4` del plan especifica: *«los tutoriales viejos de aire y agua se mudan al Archivo (`motion`/`shape`)»*, pero `00_resumen.md` (§5) y `04_planta_baja.svg` (`165°, r = 4,2 m`) mantienen `WaterBasin` (la pila de agua con el barquito) y `Bunting` en la Planta Baja de la torre. Quitarlos de la Planta Baja (o mudarlos al Archivo) libera todo el oeste y evita que el jugador crea que debe usar la pila de agua para abrir la puerta.
   - Asimismo, `00_resumen.md` sigue correctamente `§17` y `§18` al mudar `rare` y `Alchemy` al Archivo (dejando obsoleta la nota vieja `D12` de §13/§16 en `MAPA_FLUJO_PLAN.md`).

---

## 5. Accesibilidad en VR y Legibilidad de Carteles a `2,0 m` (`FloatingNote` + `TextMeshPro`)

En Meta Quest (OpenXR), un cartel leído a **`d = 2,0 m`** de distancia subtende **`1°` de ángulo visual por cada `34,9 mm`** (`2000 mm × tan(1°) = 34,91 mm`):
- **Estándar ergonómico en VR (Meta / OpenXR):**
  - Altura mínima absoluta de letra (mayúscula / *cap-height*): **`1,0°` (`35 mm` a `2 m`)**.
  - Altura recomendada de lectura cómoda sin fatiga de vergencia/acomodación: **`1,4° a 1,8°` (`49 a 63 mm` de *cap-height* a `2 m`)**.
- **Qué hace hoy `carteles.md` vs. qué pasa en `TextMeshPro` 3D:**
  - En `TextMeshPro` de espacio de mundo (sin `Canvas`), `fontSize = 1.0` equivale a un cuerpo tipográfico donde la altura de mayúscula (*cap-height*) es **`≈ 0,072 m` (`72 mm`)** y el avance promedio por carácter es **`≈ 0,043 m × fontSize`**.
  - `carteles.md` propone `fontSizeMax = 0.72f` (`cap-height ≈ 52 mm = 1,49°` a `2 m`, que es bueno), **PERO** permite líneas de hasta **38–40 caracteres** en tarjetas de `1,35 a 1,55 m` de ancho (`rectTransform.sizeDelta.x = width - 0.16f = 1,19 a 1,39 m`).
  - Con **39 caracteres** (como tenía `carteles.md` en #13 y #14), el ancho requerido a `fontSize = 0.72` es `39 × 0,043 × 0,72 = 1,21 m` (más `<b>` en negrita que ensancha un `12 %` → **`1,35 m`**), lo que obliga a `enableAutoSizing` a **achicar la fuente a `fontSize ≈ 0,52–0,56`** (`cap-height = 37–40 mm ≈ 1,06°`), en el límite inferior de legibilidad en Quest y provocando saltos de línea indeseados (3.ª línea truncada por `TextOverflowModes.Truncate`).
- **Por qué la regla de `≤ 28 caracteres por línea` resuelve VR y evita desbordes:**
  - Con **≤ 28 caracteres visibles por línea** (y promedio de 22–26 caracteres), a `fontSize = 0.72f` el ancho del renglón es:
    $$26 \times 0,045\text{ m} \times 0,72 \approx 0,84\text{ m}$$
  - En una tarjeta de **`1,25 × 0,48 m`** o **`1,35 × 0,52 m`** (área útil `1,09 × 0,36 m` o `1,19 × 0,40 m`), **las 2 líneas entran siempre al `100 %` del `fontSizeMax = 0,72f` (`cap-height = 52 mm = 1,49°` a `2 m`)**, sin activar reducción por auto-sizing, sin partir renglones en 3 líneas y con margen de sobra en cualquier resolución de HMD.
  - **Altura de colocación (`Δy`) en VR:** En `carteles.md` los carteles están a `Δy = 1,65–1,85 m`. Como la cámara en PC está a `~1,60 m` y en VR depende de la estatura real (`1,50–1,75 m`), conviene fijar **`Δy = 1,55–1,65 m`** para que el jugador en VR no tenga que levantar el cuello (`pitch > 0°` cansa en VR; el ángulo óptimo de lectura es `-5° a -10°` bajo el horizonte visual).

---

## 6. Propuesta Concreta de «Plano de Jugador» por Piso (12 a 18 elementos)

**Reglas de diseño gráfico para los 4 SVG de jugador:**
1. **Eliminar el panel técnico lateral de `380 px`:** El plano pasa a un formato limpio (`900 × 820 px` o centrado en `1200 × 820 px` con una tarjeta de leyenda compacta de solo **4 ítems**).
2. **Eliminar las 4 capas de ruido:**
   - Cero rectángulos/arcos verdes de «Zona libre: X × Y m».
   - Cero líneas de cota internas (`r = 4,80 m`, `Ø 9,60 m`, `3,20 m`) ni textos de ángulos/radios (`(325° · r=3,6m)`).
   - Cero notas editoriales explicando qué se borró de versiones anteriores (*«sin Signos raros»*, *«sin Portal del Estudio»*).
   - Cero proyecciones simultáneas de rampas de otros pisos que no hacen al recorrido del nivel actual.
3. **Jerarquía visual en 4 niveles:**
   - **Nivel 1 (Flujo):** Entrada (`LLegada` / `Spawn`), línea única de recorrido (`stroke-width="5"`, `#58d68d`) y Salida (`Bajada` / `Puerta Sur`).
   - **Nivel 2 (Bloqueo obligatorio):** Obstáculo en color de alto contraste (`#e74c3c` / `#c678ff` / `#d35400`) con rótulo de **2–3 palabras** (`14–15 px`, `font-weight="800"`).
   - **Nivel 3 (Estaciones de aprendizaje):** Icono grande + etiqueta de **2–4 palabras** (`13–14 px`).
   - **Nivel 4 (Carteles):** Círculos numerados amarillos (`#f4d03f`, `r = 14 px`) ubicados **sobre** la línea de recorrido, antes de cada estación.

---

### 6.1 Plano de Jugador — Nivel 1: Terraza (`01_terraza.svg`) — **15 elementos**

| # | Tipo de elemento | Nombre / Texto visible en el plano | Ubicación en el plano (`ang°`, `r` / `x, y`) | Rol visual |
|---:|---|---|---|---|
| 1 | Geometría base | Contorno de terraza (`r = 8,2 m`), disco central cerrado (`r = 4,8 m`) y hueco de rampa (`180°..48°`) | Centro `(400, 410)` | Contenedor limpio sin cotas |
| 2 | Bloqueo pasivo | **Valla alta** (línea roja gruesa que cierra el oeste) | `115°`, `r = 4,8..8,2 m` `(290, 175)` | Explica de un vistazo por qué se va al Este |
| 3 | Hito de inicio | Icono + rótulo **`INICIO (Spawn)`** | `90°`, `r = 6,8 m` `(400, 138)` | Punto de partida |
| 4 | Interacción | Icono + rótulo **`Palanca: Saltear`** + Cartel **`①`** | `102°`, `r = 7,4 m` `(338, 120)` | Salida rápida opcional |
| 5 | Cartel | Círculo **`②`** (`WASD · Mouse`) | `72°`, `r = 6,8 m` `(484, 151)` | Sobre la línea de avance inicial |
| 6 | Ruta | **Flecha verde continua de recorrido** (sentido horario `90° → 25° → 325° → 195° → 180°`) | Corredor `r = 6,6 m` | Guía principal de lectura |
| 7 | Cartel | Círculo **`③`** (`C · Agacharse`) | `40°`, `r = 6,6 m` `(602, 240)` | `1,75 m` antes de la viga |
| 8 | Obstáculo 1 | Barra naranja + rótulo **`1. Viga baja (C)`** + punto de farol testigo | `25°`, `r = 4,8..8,2 m` `(635, 300)` | Primer ejercicio obligatorio |
| 9 | Cartel | Círculo **`④`** (`Espacio · Saltar`) | `342°`, `r = 6,6 m` `(651, 492)` | `1,95 m` antes del muro |
| 10 | Obstáculo 2 | Barra gris + rótulo **`2. Muro bajo (Espacio)`** + punto de farol testigo | `325°`, `r = 4,8..8,2 m` `(613, 559)` | Segundo ejercicio obligatorio |
| 11 | Cartel | Círculo **`⑤`** (`Reja cerrada`) | `212°`, `r = 5,5 m` `(213, 527)` | `1,65 m` antes de la reja |
| 12 | Obstáculo 3 | Línea violeta punteada + rótulo **`3. Reja mágica`** | `195°`, `r = 4,8..6,2 m` `(187, 467)` | Puerta que se abre al cumplir 1 y 2 |
| 13 | Salida | Flecha naranja en la rampa + rótulo **`BAJA A PISO 2`** | `180° → 90°` `(181, 395)` | Meta del piso |
| 14 | Orientación | **Brújula mínima (`N`)** y **Barra de escala (`5 m`)** | Esquinas `(65, 85)` y `(40, 775)` | Referencia espacial |
| 15 | Cabecera / Leyenda | **Título + minileyenda de 4 iconos** (`Camino`, `Obstáculo`, `Cartel ①–⑤`, `Bajada`) | Arriba a la derecha `(830, 50)` | Contexto en 1 segundo |

---

### 6.2 Plano de Jugador — Nivel 2: Piso 2 (`02_piso2.svg`) — **16 elementos**

> **Cambio clave de jerarquía visual:** La ruta verde obligatoria (`0° → Sur → Pila 198° → Rampa 180°`) tiene el doble de grosor (`5 px`) que el área de práctica opcional en el disco central, que se agrupa bajo un único rótulo de zona suave (`Práctica opcional`).

| # | Tipo de elemento | Nombre / Texto visible en el plano | Ubicación en el plano (`ang°`, `r` / `x, y`) | Rol visual |
|---:|---|---|---|---|
| 1 | Geometría base | Muro circular (`r = 6,2 m`), hueco de rampa norte (`48°..180°`) y baranda naranja (`48°..205°`) | Centro `(400, 410)` | Define por dónde se puede caminar |
| 2 | Entrada | Flecha verde clara + rótulo **`LLEGA DE TERRAZA`** + icono Farol Nivel 1 | `0°` Este `(615, 395)` | Punto de entrada al piso |
| 3 | Ruta principal | **Flecha verde gruesa obligatoria** (`0° → 270° → 216° → 198° → 180°`) pasando **por encima** del Cartel `⑩` | Arco sur `r = 5,3 m` | Corrige el desvío de `02_piso2.svg` |
| 4 | Cartel opcional | Círculo **`⑥`** | `8°`, `r = 3,1 m` `(523, 393)` | Antes de la mesa del lienzo |
| 5 | Estación opcional A | Icono mesa + rótulo **`Mesa con lienzo (Opcional)`** | `20°`, `r = 2,5 m` `(494, 376)` | Punto de partida de práctica |
| 6 | Cartel opcional | Círculo **`⑦`** | `48°`, `r = 2,7 m` `(472, 330)` | Antes del Marco 1 |
| 7 | Estación opcional 1 | Icono + rótulo **`Marco 1: Flotar (E)`** + farolito | `60°`, `r = 3,1 m` `(462, 303)` | Ejercicio opcional 1 |
| 8 | Cartel opcional | Círculo **`⑧`** | `95°`, `r = 2,7 m` `(391, 302)` | Antes del Marco 2 |
| 9 | Estación opcional 2 | Icono + rótulo **`Marco 2: Rotar y dar vuelta`** + farolito | `110°`, `r = 3,1 m` `(358, 293)` | Ejercicio opcional 2 |
| 10 | Cartel opcional | Círculo **`⑨`** | `328°`, `r = 3,8 m` `(529, 491)` | Antes de la caja de tiro |
| 11 | Estación opcional 3 | Icono caja + diana + rótulo **`Diana: Arrojar (Q)`** | `315°`, `r = 3,5 → 6,1 m` `(573, 583)` | Ejercicio opcional 3 |
| 12 | Cartel obligatorio | Círculo **`⑩`** (`E mover · Q arrojar`) **sobre la ruta principal** | `216°`, `r = 5,35 m` `(227, 536)` | `1,75 m` antes de la pila |
| 13 | Obstáculo obligatorio | Icono de cajas/barril + rótulo **`PILA DE CAJAS (Despejar)`** | `198°`, `r = 4,8..6,15 m` `(188, 470)` | Único bloqueo real del piso |
| 14 | Salida | Flecha naranja + rótulo **`BAJA A PISO 1`** | `180°` Oeste `(181, 395)` | Meta del piso |
| 15 | Orientación | **Brújula (`N`)** y **Barra de escala (`5 m`)** | `(65, 85)` y `(40, 775)` | Referencia espacial |
| 16 | Cabecera / Leyenda | **Título + distinción visual:** `Verde = Obligatorio` vs. `Celeste = Práctica opcional` | `(830, 50)` | Evita confundir los 3 opcionales con el bloqueo |

---

### 6.3 Plano de Jugador — Nivel 3: Piso 1 (`03_piso1.svg`) — **14 elementos**

| # | Tipo de elemento | Nombre / Texto visible en el plano | Ubicación en el plano (`ang°`, `r` / `x, y`) | Rol visual |
|---:|---|---|---|---|
| 1 | Geometría base | Muro circular (`r = 6,2 m`), hueco de rampa norte (`48°..180°`) y baranda (`48°..205°`) | Centro `(400, 410)` | Contenedor limpio |
| 2 | Entrada | Flecha verde clara + rótulo **`LLEGA DE PISO 2`** + Farol Nivel 2 | `0°` Este `(615, 395)` | Punto de llegada |
| 3 | Ruta en serie | **Flecha verde continua** que une `0° → ① Mesa Tinta → ② Libro → ③ Atril → Cortina → 180°` | Hemisferio sur | Secuencia lineal 1 → 2 → 3 |
| 4 | Cartel | Círculo **`⑪`** | `340°`, `r = 4,1 m` `(554, 466)` | Antes de la mesa de tinta |
| 5 | Estación 1 | Icono mesa + rótulo **`1. Pluma, Tinta y Cinturón`** | `325°`, `r = 3,6 m` `(518, 493)` | Equipar herramientas |
| 6 | Cartel | Círculo **`⑫`** | `295°`, `r = 2,2 m` `(437, 490)` | Antes de la mesa del libro |
| 7 | Estación 2 | Icono mesa/libro + rótulo **`2. Libro: Tomo I (Viento)`** | `270°`, `r = 1,6 m` `(400, 474)` | Aprender el glifo de viento |
| 8 | Cartel | Círculo **`⑬`** | `238°`, `r = 2,9 m` `(339, 508)` | Antes del atril guiado |
| 9 | Estación 3 | Icono atril/papel + rótulo **`3. Lienzo guiado (Viento)`** | `220°`, `r = 2,6 m` `(320, 477)` | Dibujar y apuntar |
| 10 | Vector de hechizo | Flecha celeste punteada + rótulo **`Apuntar viento`** | `(302, 474) → (202, 467)` | Muestra hacia dónde apuntar el lienzo |
| 11 | Obstáculo obligatorio | Zigzag bordó + rótulo **`4. CORTINA PESADA`** | `195°`, `r = 4,85..6,14 m` `(188, 467)` | Obstáculo que vuela con viento |
| 12 | Salida | Flecha naranja + rótulo **`BAJA A PLANTA BAJA`** | `180°` Oeste `(181, 395)` | Meta del piso |
| 13 | Orientación | **Brújula (`N`)** y **Barra de escala (`5 m`)** | `(65, 85)` y `(40, 775)` | Referencia espacial |
| 14 | Cabecera / Leyenda | **Título + 4 pasos resumidos en 1 línea cada uno** | `(830, 50)` | Lectura instantánea |

---

### 6.4 Plano de Jugador — Nivel 4: Planta Baja (`04_planta_baja.svg`) — **13 elementos**

> **Aplicación estricta de la decisión de Tamara:** En el plano de jugador de Planta Baja **se eliminan por completo los rótulos y explicaciones del Contrapeso (`284°`) y de la Piedra (`296°`)** (quedan a lo sumo como geometría muda del muro o en la ficha técnica para desarrolladores). El plano muestra **una única solución guiada**: el lienzo agarrable apuntado al pestillo de la puerta.

| # | Tipo de elemento | Nombre / Texto visible en el plano | Ubicación en el plano (`ang°`, `r` / `x, y`) | Rol visual |
|---:|---|---|---|---|
| 1 | Geometría base | Muro circular (`r = 6,2 m`), talud exterior (`r = 8,6 m`), alfombra hacia el sur y rampa norte (`180° → 0°`) | Centro `(400, 410)` | Contenedor arquitectónico limpio |
| 2 | Entrada | Flecha en rampa norte + rótulo **`VIENE DE PISO 1`** + Farol Nivel 3 | `0°` Este `(615, 395)` | Desembarco en Planta Baja |
| 3 | Estación opcional | Icono mesa + rótulo **`Mesa: Tinta y cuadernos`** | `40°`, `r = 2,4 m` `(474, 348)` | Reposición opcional fuera del paso |
| 4 | Estación de lectura | Icono atril + rótulo **`1. Libro: Los cuatro elementos`** | `320°`, `r = 2,6 m` `(480, 477)` | En el camino natural desde la rampa |
| 5 | Ruta principal | **Flecha verde continua** (`0° → Atril Libro 320° → Cartel ⑭ 285° → Atril Lienzo 262° → Puerta Sur 270°`) | Este → Centro-Sur → Sur | Flujo único y despejado |
| 6 | Cartel | Círculo **`⑭`** (`Dibujá viento · Apuntá al pestillo`) | `285°`, `r = 2,9 m` `(435, 505)` | Reubicado `1,2 m` **antes** del atril sobre la curva |
| 7 | Estación guiada | Icono atril/papel + rótulo **`2. Lienzo guiado (Viento)`** | `262°`, `r = 3,0 m` `(383, 529)` | Única estación de resolución mostrada |
| 8 | Vector de hechizo | Flecha celeste punteada + rótulo **`Apuntar al pestillo`** | `(378, 546) → (348, 624)` | Conecta el lienzo con el pestillo |
| 9 | Objetivo | Icono de barra de madera + rótulo **`3. Pestillo de la puerta`** *(sin rótulos en contrapeso ni piedra)* | `256°`, `r = 5,85 m` `(343, 637)` | Único blanco rotulado en la puerta |
| 10 | Puerta | Hoja de madera con arco de apertura + Farol Nivel 4 + rótulo **`PUERTA SUR`** | `270°`, `r = 6,2 m` `(400, 658)` | Salida física de la torre |
| 11 | Fin del tutorial | Flecha verde exterior + rótulo **`SALIDA AL NEXO (Fin del tutorial)`** | `270°`, `r > 7,5 m` `(400, 768)` | Cierre del recorrido |
| 12 | Orientación | **Brújula (`N`)** y **Barra de escala (`5 m`)** | `(65, 85)` y `(40, 775)` | Referencia espacial |
| 13 | Cabecera / Leyenda | **Título + 3 pasos guiados** (`1. Leer libro · 2. Dibujar viento · 3. Apuntar al pestillo y salir`) | `(830, 50)` | Cero sobrecarga cognitiva |

---

## 7. Reescritura Final de los 14 Carteles (`≤ 2 líneas`, `≤ 28 caracteres/línea`)

> **Criterios verificados carácter por carácter:**
> - **Largo máximo estricto:** **≤ 28 caracteres visibles por línea** (contando espacios y signos de puntuación; sin contar los tags `<b>` y `</b>`). El renglón más largo de toda la tabla tiene **27 caracteres**.
> - **Máximo 2 líneas** por tarjeta (`\n`).
> - **Tamaño de tarjeta en metros (`ancho × alto`):** Calculado para `fontSizeMax = 0,72f` (`cap-height ≈ 52 mm = 1,49°` a `2 m` en VR), con margen interior de `0,08 m` en X y `0,06 m` en Y, garantizando **cero reescalado hacia abajo y cero desborde**.
> - **Altura sobre el piso (`Δy`):** Ajustada a **`1,60 m`** (en vez de `1,85 m`) para lectura ergonómica tanto en PC como en VR sin levantar la cabeza.

| # | Piso | Nombre en código | Línea 1 exacta (con tags `<b>`) | Línea 2 exacta (con tags `<b>`) | Car. visibles (L1 / L2) | Tamaño tarjeta (`ancho × alto`) | Posición (`ang°`, `r`, `Δy`) y orientación (`viewer`) |
|---:|---|---|---|---|---:|---|---|
| **1** | Terraza | `Note_Tutorial_Skip` | `<b>E</b> · Saltear tutorial` | `Ir directo al Nexo` | **20 / 18** | `1,15 × 0,46 m` | `100°, r = 7,40 m, Δy = 1,60 m` · Mira al Spawn `(90°, r = 6,8 m)` |
| **2** | Terraza | `Note_Tutorial_Move` | `<b>WASD</b> caminar · <b>Mouse</b> mirar` | `Seguí por el balcón este` | **26 / 24** | `1,30 × 0,50 m` | `72°, r = 6,80 m, Δy = 1,60 m` · Mira al Spawn `(90°, r = 6,8 m)` |
| **3** | Terraza | `Note_Tutorial_Crouch` | `<b>C</b> · Agachate para pasar` | `por debajo de la viga` | **23 / 21** | `1,25 × 0,48 m` | `40°, r = 6,60 m, Δy = 1,60 m` · Mira a `(58°, r = 6,8 m)` (`1,75 m` antes de viga en `25°`) |
| **4** | Terraza | `Note_Tutorial_Jump` | `<b>Espacio</b> · Saltá el muro` | `de piedra para seguir` | **23 / 21** | `1,25 × 0,48 m` | `342°, r = 6,60 m, Δy = 1,60 m` · Mira a `(2°, r = 6,6 m)` (`1,95 m` antes de muro en `325°`) |
| **5** | Terraza | `Note_Tutorial_Gate` | `Reja cerrada` | `Completá agacharse y saltar` | **12 / 27** | `1,30 × 0,50 m` | `212°, r = 5,50 m, Δy = 1,60 m` · Mira a `(232°, r = 6,0 m)` (`1,65 m` antes de reja en `195°`) |
| **6** | Piso 2 | `Note_Tutorial_Free` | `Opcional · <b>E</b> agarrá lienzo` | `Otra <b>E</b> lo deja flotando` | **26 / 23** | `1,30 × 0,50 m` | `8°, r = 3,10 m, Δy = 1,60 m` · Mira al desembarco `(0°, r = 5,5 m)` |
| **7** | Piso 2 | `Note_Tutorial_Float` | `Opcional 1 · Flotar lienzo` | `Soltalo con <b>E</b> en el marco` | **26 / 25** | `1,30 × 0,50 m` | `48°, r = 2,70 m, Δy = 1,60 m` · Mira a `(25°, r = 2,6 m)` (`0,75 m` antes del Marco 1 en `60°`) |
| **8** | Piso 2 | `Note_Tutorial_Upright` | `<b>Clic der. + mouse</b> rotar` | `<b>F</b> dar vuelta · <b>E</b> flotar` | **23 / 23** | `1,30 × 0,50 m` | `95°, r = 2,70 m, Δy = 1,60 m` · Mira a `(72°, r = 2,9 m)` (`0,85 m` antes del Marco 2 en `110°`) |
| **9** | Piso 2 | `Note_Tutorial_Target` | `Opcional 3 · Tiro al blanco` | `<b>E</b> agarrá caja · <b>Q</b> arrojala` | **27 / 26** | `1,30 × 0,50 m` | `328°, r = 3,80 m, Δy = 1,60 m` · Mira a `(350°, r = 4,8 m)` (`0,90 m` antes de caja en `315°`) |
| **10** | Piso 2 | `Note_Tutorial_Grab` | `<b>E</b> agarrar o soltar` | `<b>Q</b> arrojar · Despejá el paso` | **18 / 27** | `1,30 × 0,50 m` | `216°, r = 5,35 m, Δy = 1,60 m` · Mira a `(238°, r = 5,3 m)` (`1,75 m` antes de pila en `198°`) |
| **11** | Piso 1 | `Note_Tutorial_Ink` | `<b>E</b> equipá pluma y tintero` | `<b>R</b> recargar tinta` | **24 / 16** | `1,25 × 0,48 m` | `340°, r = 4,10 m, Δy = 1,60 m` · Mira al desembarco `(0°, r = 5,5 m)` (`1,15 m` antes de mesa `325°`) |
| **12** | Piso 1 | `Note_Tutorial_Book` | `<b>E</b> leé Tomo I: el viento` | `<b>T</b> abre tu grimorio` | **23 / 18** | `1,25 × 0,48 m` | `295°, r = 2,20 m, Δy = 1,60 m` · Mira a `(325°, r = 3,4 m)` (`1,05 m` antes de mesa `270°`) |
| **13** | Piso 1 | `Note_Tutorial_Steps` | `Dibujá viento (<b>Tab</b> vista)` | `<b>E</b> agarrá y apuntá a la tela` | **25 / 27** | `1,35 × 0,52 m` | `238°, r = 2,90 m, Δy = 1,60 m` · Mira a `(268°, r = 2,0 m)` (`0,90 m` antes de atril `220°`) |
| **14** | Planta baja | `Note_Tutorial_Door` | `Dibujá viento en el lienzo` | `<b>E</b> apuntá al pestillo` | **26 / 20** | `1,30 × 0,50 m` | `285°, r = 2,90 m, Δy = 1,60 m` · Mira a `(320°, r = 2,8 m)` (`1,20 m` antes de atril `262°`) |

---

## 8. Lista Priorizada de Acciones (`P0` / `P1` / `P2`)

### P0 — Bloqueantes (Diseño, Código y Legibilidad)
1. **Simplificar los 4 SVG al diseño de «Plano de Jugador» (13–16 elementos por lámina):** Quitar el panel técnico derecho de `380 px`, las cajas verdes de áreas libres, las cotas internas, las etiquetas de coordenadas `(ang° · r)` y los rótulos de las trabas de agua y tierra en `04_planta_baja.svg` (Sección 6).
2. **Aplicar la tabla maestra de los 14 carteles con ≤ 28 caracteres/línea (Sección 7):** Unificar `carteles.md`, `00_resumen.md` y `MapTutorial.cs` con los textos exactos de la Sección 7 y `TextWrappingModes.Normal` obligatorio.
3. **Corregir la trampa de «Mantené C» (#3) vs. `Prone` (`0,35 s`):** Cambiar el texto del Cartel #3 a `<b>C</b> · Agachate para pasar / por debajo de la viga` y evitar en `PlayerInputReader.cs` que mantener `C` mientras se camina deje al jugador atrapado en `prone = true` sin poder saltar el muro siguiente.
4. **Resolver el orden disparo-agarre en `TutorialPaperGuide` (Piso 1 y Planta Baja):** Como cerrar el anillo lanza el hechizo al instante, o bien `TutorialPaperGuide` muestra la flecha de apunte (`AimGuide`) también sobre el atril/flotando (con el atril ya orientado hacia la cortina en `195°` y hacia el pestillo en `256°`), o bien se instruye a orientar el lienzo con `E` antes de cerrar el anillo.
5. **Cerrar la baranda interior de la Terraza (`r = 4,8 m`) y ajustar `TerraceRules.OnLine`:** Si la viga (`25°`) y el muro (`325°`) solo cubren `r = 4,8..8,2 m`, `MapTutorial.Terrace` debe generar la baranda circular completa en `r = 4,8 m` y `TerraceRules.OnLine` debe validar `along >= 4.8f && along <= Radius`.
6. **Ajustar `TowerBuilder.RoofMesh()` y `BeamsMesh()` al nuevo radio (`OuterRadius = 6,8 m`, `RampTurn = 180°`):** Escalar el aro de soporte de los postes a `r = 6,3..6,8 m`, el alero a `9,3 → 5,8 m`, e invertir el arco de vigas de `BeamsMesh()` para que no atraviesen la rampa norte.

### P1 — Importantes (Limpieza de Escena, Física y Documentación)
7. **Limpiar objetos huérfanos no listados en `00_resumen.md`:**
   - En `CanvasDesigns.cs`: quitar `DraftingTable` (`217°, r = 2,0 m`, que choca con el atril guiado del Piso 1), `Easel_Study`, `WallBoard_Library` y `BigCanvas_Library`.
   - En `MapDetails.cs`: quitar las 2 velas de `LibraryDesk` (`TowerAmbience`), la estampa `Stamp_M_5` (`Stamps`, prohibida en la torre) y `Paper_S`/`Paper_M` (`Papers`).
   - En `TowerBuilder.Interior()`: quitar `ReadingTable` y sus sillas además de `LibraryDesk`.
8. **Fijar `rb.isKinematic = true` inicial en los lienzos sobre atriles inclinados `25°` (`TutorialPaper_Wind` y `TutorialPaper_Door`):** Evita que se deslicen o caigan por gravedad al iniciar la escena antes de que el jugador los agarre con `E`.
9. **Sincronizar `Docs/GAME_DESIGN.md` y `GrimoireTexts.cs` con los controles confirmados:** Corregir en el texto de `GAME_DESIGN.md` las menciones viejas a `G` (ahora `E`), `Tab` para grimorio (ahora `T`), `V` para vista de lienzo (ahora `Tab`, siendo `V` cambio de tinta del cinturón) y `Z` para tumbarse.
10. **Unificar ángulos entre `00_resumen.md` y `carteles.md`:** Alinear las diferencias de `2°–15°` en Piso 2 (estaciones en `20°/60°/110°/315°`), Piso 1 (carteles en `340°/295°/238°`) y Planta Baja (atril `elements` en `320°`, atril guiado en `262°`, pestillo en `256°`, contrapeso en `284°`, piedra en `296°`).

### P2 — Pulido Visual y Ergonomía VR
11. **Bajar la altura de las tarjetas (`FloatingNote`) a `Δy = 1,60 m`:** Mejora el ángulo de lectura en VR (`-5°`) y evita levantar la vista frente a las estaciones.
12. **Separar la documentación en dos vistas:** Dejar `01_terraza.svg` .. `04_planta_baja.svg` exclusivamente como planos limpios de jugador (13–16 elementos) y mover todas las cotas radiales, áreas en `m²` y ángulos de código a tablas en `00_resumen.md`.

---

## PREGUNTAS

Ninguna.
