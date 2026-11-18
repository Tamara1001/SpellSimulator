---
tags: [doc, auditoria, magia, modificadores]
---
# Auditoría de Elementos × Modificadores de Hechizos

Auditoría técnica del pipeline de compilación y manifestación mágica (*SpellSimulator* vs. canon de *Witch Hat Atelier*).
Fuentes de verdad auditadas:
- Contratos y compilación: `Docs/MAGIC_SYSTEM.md`, `Docs/GLYPHS.md`, `Assets/_Project/Scripts/Compiler/` (`SpellIR.cs`, `SpellCompiler.cs`, `SpellPlan.cs`, `PlanResolver.cs`, `SemanticRules.cs`).
- Módulo de salida física y visual: `Assets/_Project/Scripts/Output/` (`SpellOutputController.cs`, `SpellManifestation.cs`, `MagicField.cs`, `ElementLook.cs`, `Bodies/SpellBodies.cs`, `RockSpell.cs`, `EarthShapes.cs`, `WaterSpell.cs`, `WindSpell.cs`, `CrystalSpell.cs`) y `Assets/_Project/Scripts/Reactions/` (`MagicWorld.cs`, `ElementReactive.cs`).
- Datos de glifos: `Assets/_Project/Data/glyph-class-to-idx.json`, `Assets/_Project/Data/Lore/glifos.json`.
- Canon de referencia: Wiki *Signs Explained* / *Sigils Explained* y `wha-spell-simulator`.

---

## 1. Tabla de Modificadores y Parámetros: Transporte y Uso en Output

Esta tabla audita cómo cada variable escalar del dibujo, parámetro de `SpellIR` y signo modificador es transportado y consumido o ignorado por el subsistema `Output`.

| Modificador / Signo | Campo en `SpellIR` / `SpellPlan` | Estado en Output | Archivo y Línea de Consumo | Efecto / Comportamiento según Canon (Wiki WHA) |
|---|---|---|---|---|
| **Radio de Anillo (Extensivo)** | `SpellIR.sealRadius` | **USADO** | `SpellOutputController.cs:63`, `MagicField.cs:85`, `RockSpell.cs:46`, `EarthShapes.cs:73`, `WaterSpell.cs:100`, `CrystalSpell.cs:28` | Ley extensiva (R-02): El área del anillo escala el caudal, volumen y fuerza total (no la velocidad de salida). En el juego escala `frame.Radius`, acople físico `areaFactor` y dimensiones de cuerpos. |
| **Tamaño de Sigilo (Intensivo)** | `SpellIR.sizeRatio`, `primarySizeNorm` | **IGNORADO (absorbido)** | No se leen directamente; absorbidos en `strength` y `effectScale` (`SpellCompiler.cs:68-69`) | El tamaño del sigilo respecto a su norma calibra la estabilidad del núcleo elemental y potencia base. |
| **Escala de Efecto** | `SpellIR.effectScale` | **USADO** | `SpellOutputController.cs:65` (`frame.Scale = frame.Radius * effectScale`), `SpellManifestation.cs:51`, `MagicField.cs:95` | Factor multiplicador de volumen de la manifestación visual y alcance del campo físico. |
| **Fuerza (Intensiva)** | `SpellIR.force` | **USADO** | `SpellManifestation.cs:53`, `MagicField.cs:87, 166, 423`, `RockSpell.cs:84`, `WaterSpell.cs:100, 107` | Ley intensiva (R-02): Fija la velocidad del flujo y partículas (`flowSpeed`), impulso de ráfaga (`dv`), velocidad de eyección de rocas y duración de inundación. |
| **Fuerza Nuclear / Brillo** | `SpellIR.strength` | **USADO (Parcial)** | `SpellManifestation.cs:70` (partículas de ráfaga), `SpellManifestation.cs:153` (intensidad lumínica) | Intensidad del núcleo mágico. Solo modula cantidad de partículas del burst y la luz puntual. |
| **Rango / Alcance** | `SpellIR.range` | **USADO** | `SpellManifestation.cs:78, 91`, `MagicField.cs:95, 97, 145`, `EarthShapes.cs:71` | Longitud del chorro (`ConeLength`), radio de abanico, alcance de exposición y dimensiones de muros/puentes. |
| **Apertura / Cono (Spread)** | `SpellIR.spread` | **USADO PARCIAL / IGNORADO** | `MagicField.cs:92, 104` (física de Viento); **IGNORADO** en `SpellManifestation.cs:81` y en Fuego, Agua, Tierra y Luz | Grado de dispersión angular del flujo. En visuales usa `focus` fijo y en física solo el viento ensancha su cono; los otros 4 elementos ignoran `spread`. |
| **Enfoque Escalar** | `SpellIR.focus` | **USADO (Parcial)** | `SpellManifestation.cs:81` (`cone = Lerp(18, 3, ...)` en jet); **IGNORADO** en `MagicField.cs` | Concentración angular. `SpellManifestation` lo usa para estrechar el cono de partículas; la física usa `plan.Focus`. |
| **Duración** | `SpellIR.duration` | **USADO** | `SpellManifestation.cs:47` (`bodySeconds = Clamp(duration - 1.5, ...)`), `MagicField.cs:130` | Tiempo de vida del cuerpo activo del hechizo antes de pasar a la fase de liberación y desvanecimiento. |
| **Gravedad Modificada** | `SpellIR.gravity` | **IGNORADO** | Ninguno (no se lee en `SpellManifestation.cs`, `MagicField.cs` ni `Bodies`) | Reducción de caída por signos de levitación sin llegar a orbe (`1 - levitation * 0.42`). Partículas usan `look.gravity` fijo y cuerpos usan `Physics.gravity`. |
| **Calidad de Trazo** | `SpellIR.quality` | **USADO** | `SpellManifestation.cs:54` (densidad de partículas), `MagicField.cs:91, 96` (acople físico y arrastre) | Prolijidad del dibujo (R-10): Modula densidad visual y eficiencia de acople con los cuerpos rígidos del entorno. |
| **Estabilidad** | `SpellIR.stability` | **IGNORADO** | Ninguno | Regularidad del trazo. En el canon determina si el sello tiembla, chisporrotea o fluctúa. `Output` no lo evalúa. |
| **Prolijidad (Neatness)** | `SpellIR.neatness` | **IGNORADO en Output** | Solo consumido en compilador (`SpellCompiler.cs:77`) | Coherencia geométrica. En `Output` se ignora directamente a favor de `quality`. |
| **Dirección Superficie** | `SpellIR.direction`, `xTiltDeg`, `yTiltDeg`, `directionCoherence` | **IGNORADO** | Ninguno en `Output/` | Vector 3D calculado en el compilador anterior a la capa Plan. `Output` lee exclusivamente `frame.Normal` y `plan.Aim`. |
| **Tinta Poderosa** | `SpellIR.inkPower` | **USADO** | `MagicField.cs:89-97`, `RockSpell.cs:46, 94`, `EarthShapes.cs:66`, `WaterSpell.cs:28-29`, `WindSpell.cs:22, 33`, `CrystalSpell.cs:28` | Multiplicador alquímico (2.5×): Escala masa de rocas, velocidad de viento, volumen de gotas de agua y alcance. |
| **Tinta Resistente (Waterproof)** | `SpellIR.waterproof` | **IGNORADO** | `WaterSpell.cs:15` («la tinta resistente no cambia nada») | Sellos que operan bajo el agua o toleran ser mojados sin correrse ni cancelarse. Totalmente inerte en Output. |
| **Columna (`column`)** | `plan.Aim`, `Dispersion`, `Circulation`, `Primitives` | **USADO** | `SpellManifestation.cs:75-110`, `MagicField.cs:428-485`, `RockSpell.cs:40`, `EarthShapes.cs:69`, `WindSpell.cs:23` | Motor direccional (R-05): Opuestos hacia adentro = chorro (`Jet`); hacia afuera = abanico (`Fan`); molinete = vórtice (`Vortex`). En Tierra genera Muro/Puente. *(Nota: en roca sólida apaga partículas en `SpellManifestation.cs:73`)*. |
| **Dispersión (`dispersion`)** | `plan.Dispersion`, `plan.Leak`, `Primitives.Fan` | **USADO** | `SpellManifestation.cs:47, 87`, `MagicField.cs:448`, `EarthShapes.cs:76` | Fuga lenta y derame plano (R-08): +50% duración del cuerpo, abanico radial suave y estacas radiales en Tierra. |
| **Levitación (`levitation`)** | `plan.Hold`, `Primitives.Hold` | **USADO** | `SpellManifestation.cs:112-125`, `MagicField.cs:487-501`, `RockSpell.cs:83` | Par de fuerzas (R-13): Suspende un orbe a cierta altura sobre el papel con giro y resorte amortiguador; la roca de tierra queda flotando. |
| **Flotar (`float`)** | `plan.Hold` / `levitation` | **USADO (como alias)** | Mismo flujo que `levitation` (`PlanResolver.cs:185`) | Levitación no direccional. En el simulador funciona como duplicado de levitación. |
| **Atracción (`pull`)** | `plan.Intake`, `Primitives.Intake` | **USADO** | `SpellManifestation.cs:127-141`, `MagicField.cs:503-522` | Acople ambiental (R-07): Succión de materia del *mismo elemento* que ya existe en el entorno (`IElementMatter`). |
| **Convergencia (`convergence`)** | `plan.Focus` | **USADO** | `SpellManifestation.cs:81-82`, `MagicField.cs:103, 444`, `EarthShapes.cs:65`, `RockMath.cs:42` | Lente concentradora (R-13): Chorro más angosto y rápido. Con Tierra (`Focus >= 1.5`) las estructuras se vuelven **permanentes** (no se hunden). |
| **Aplastar (`crush`)** | `plan.Crush`, `MagicField.CrushPower` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:54, 213-222`, `MagicWorld.cs:80`, `RockSpell.cs:39` | Fuerza demoledora: +50% daño a vivos, rompe reactivos y en Tierra ejecuta *Wall Breaker* (agujero en mallas). **Sin VFX visual propio**. |
| **Región (`region` / `directed`)** | `plan.Directed` | **IGNORADO (salvo proyectil Tierra)** | `RockSpell.cs:84` (lanza roca); **IGNORADO** en Fuego, Agua, Viento, Luz y en `SpellManifestation` | Válvula espacial: Delimita fronteras mágicas (cercos, cortinas, cortafuegos). Ignorado en 4 de 5 elementos; solo lanza rocas en Tierra. |
| **Tejido (`weave`)** | `plan.Weave` | **USADO PARCIAL / DEGRADADO** | `PlanResolver.cs:216` (suma a `Circulation`), `MagicField.cs:465` | Enlace helicoidal: Transforma el flujo en tiras o corrientes trenzadas. En el código solo suma rotación escalar al vórtice. |
| **Recolección (`collection`)** | `plan.Collection` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:238-251`; **IGNORADO** en `SpellManifestation.cs` | Reúne objetos sueltos (<60 kg) en un punto suspendido sobre el sello. **Sin VFX de partículas convergentes**. |
| **Nube (`billowing`)** | `plan.Billow` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:253-265`; **IGNORADO** en `SpellManifestation.cs` | La magia sale hinchada como nube; objetos pierden 85% de gravedad con alta resistencia viscosa. **Usa partículas genéricas**. |
| **Repetición (`repetition`)** | `plan.Repetition` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:224-236`; **IGNORADO** en `SpellManifestation.cs` | Rebobina estado local (respawnea objetos, seca agua, apaga y restaura materiales quemados). **Sin feedback visual temporal**. |
| **Frío (`cool`)** | `plan.Cool` | **USADO EN REACCIONES / IGNORADO VISUAL** | `MagicField.cs:271-283`; **IGNORADO** en `SpellManifestation.cs` | Baja temperatura de reactivos (calor negativo). **Sin partículas de escarcha, vaho ni tinte frío**. |
| **Fortalecer (`empower`)** | `plan.Empower` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:195` (`envelope *= 1 + 0.35 * Empower`); **IGNORADO** en `SpellManifestation.cs` | Refuerza caudal y aceleración física (+35%). **Sin intensificación visual de tamaño de llama ni brillo**. |
| **Enroscar (`entwine`)** | `plan.Entwine` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:286-301`; **IGNORADO** en `SpellManifestation.cs` | Fuerza tangencial y constricción radial alrededor del eje del sello. **Sin zarcillos ni espirales visibles**. |
| **Mira Fija (`focus`)** | `plan.Target` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:337-362`; **IGNORADO** en `SpellManifestation.cs` | *Sights Set*: Redirige objetos y proyectiles hacia la entidad viva más cercana en la línea de tiro. **Sin rayo guía visible**. |
| **Reunir (`gather`)** | `plan.Gather` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:304-316`; **IGNORADO** en `SpellManifestation.cs` | Succión pesada (hasta 120 kg con 14 m/s²). **Sin partículas de arrastre ni vórtice centrípeto**. |
| **Esfera (`orb`)** | `plan.Orb` | **USADO FÍSICO / IGNORADO VISUAL** | `MagicField.cs:319-334`; **IGNORADO** en `SpellManifestation.cs` | Contenedor esférico con gravedad confinada. **Sin burbuja translúcida ni membrana de retención visible**. |

---

## 2. Matriz de Interacción: Elemento × Modificadores Clave

| Elemento | Columna / Dirección | Levitación / Orbe | Atracción (Pull) | Convergencia | Aplastar (Crush) | Región |
|---|---|---|---|---|---|---|
| **Fuego** | Chorro de fuego dirigido (*Flame shot*) | Esfera de fuego suspendida (*Pyreball*) | Atrae llamas y brasas del entorno | Chorro térmico fino concentrado | +50% daño y rotura acelerada | **IGNORADO** (debería formar Muro o Anillo de Fuego) |
| **Agua** | Chorro vertical (*Watershot*) / Inundación | Burbuja de agua suspendida (*Water Orb*) | Atrae agua del lago/fuente (Ley 7) | Lanza de agua de alta presión | +50% daño y choque hidráulico | **IGNORADO** (debería formar Cortina de Agua o Lluvia localizada) |
| **Viento** | Ráfaga / Columna ascendente (*Skysoaring*) | Flotar en sustentación (*Sylph Shoes*) | Succión de masa de aire (*Grasping Wind*) | Corriente de aire supersónica estrecha | Onda de choque acústico-cinética | **IGNORADO** (debería formar Muro o Domo de Viento) |
| **Tierra** | Muro (piso) o Puente (pared) | Roca flotante en reposo (*Hover*) | Atrae rocas y cascotes sueltos | **Estructura fija permanente** (no se hunde) | ***Wall Breaker* (agujero destructivo en muros)** | **Lanza la roca como proyectil catapultado** |
| **Cristal** | Placa hexagonal lisa en piso | Cristal flotante | Atrae esquirlas | Foco de refracción | Estallido en metralla de cristal | **IGNORADO** (debería formar Cúpula de Cristal) |
| **Luz** | Haz direccional de iluminación | Lámpara flotante (*Floatglow Lamp*) | Inerte (no atrae materia pesada) | Rayo concentrado coherente | Daño radiante / destello cegador | **IGNORADO** (debería delimitar zona iluminada) |

---

## 3. Lista Priorizada de Modificadores Ignorados

### Prioridad 1: Críticos para la Jugabilidad y el Canon Físico
1. **`spell.waterproof` (Tinta Resistente):**
   - *Impacto:* No tiene ningún efecto en `Output` (`WaterSpell.cs:15`). Un sello sumergido o salpicado se anula indistintamente, impidiendo sellos subacuáticos o herramientas inmunes al agua.
2. **`region` (`directed`) en Fuego, Agua, Viento y Luz:**
   - *Impacto:* Es uno de los signos capitales del canon (la "válvula" que delimita fronteras). Actualmente solo se usa como disparador balístico en Tierra (`RockSpell.cs:84`). En los demás elementos no crea cercos, anillos (*Ring of Fire*), cortinas de viento ni áreas delimitadas (*Rainbringer*).
3. **`spell.gravity` (Gravedad escalar del SpellIR):**
   - *Impacto:* Calculado en el compilador (`SpellCompiler.cs:84`), es ignorado por completo en `SpellManifestation` y `MagicField`. Los efectos de flotabilidad gradual se pierden salvo que se active la primitiva estricta `Hold`.
4. **`spell.spread` en Fuego, Agua, Tierra, Luz y VFX:**
   - *Impacto:* `SpellManifestation.cs:81` ignora `spread` al definir el cono de partículas, y `MagicField.cs:104` solo lo contempla para el viento. Un lanzallamas o chorro de agua no puede ensancharse con signos de dispersión.

### Prioridad 2: Deuda Visual y Sensorial (Falta de Feedback en Partículas)
5. **Los 8 Signos Especiales sin Manifestación Visual en `SpellManifestation`:**
   - `crush` (sin esquirlas ni chispas de rotura), `orb` (sin membrana esférica visible), `billowing` (sin volutas esponjosas de nube), `cool` (sin escarcha/vaho), `empower` (sin sobretensión/brillo aumentado), `entwine` (sin zarcillos espirales), `focus` (sin haz guía de apuntado), `repetition` (sin distorsión temporal o inversión de partículas) y `gather`/`collection` (sin partículas de convergencia centrípeta).
6. **`weave` degradado a giro escalar simple:**
   - *Impacto:* En lugar de renderizar chorros entrelazados en doble hélice o cintas mágicas, solo añade un valor escalar a la rotación del vórtice (`PlanResolver.cs:216`).

### Prioridad 3: Parámetros Huérfanos y Deuda Técnica en `SpellIR`
7. **Campos vectoriales redundantes (`direction`, `xTiltDeg`, `yTiltDeg`, `directionCoherence`):**
   - Calculados en `SpellCompiler.cs:86`, están 100% desvinculados de `Output`, que utiliza `frame.Normal` y `plan.Aim`.
8. **Parámetros de prolijidad secundarios (`stability`, `neatness`):**
   - No tienen traducción visual (como parpadeo de partículas o chisporroteo inestable).

---

## 4. Propuesta de Implementación por Modificador Ignorado (Conceptual, sin código)

### 4.1. Tinta Resistente (`waterproof`)
- **Módulo responsable:** `Reactions/MagicWorld.cs` y `Canvas/DrawingCanvas.cs` / `SealBreaker.cs`.
- **Propuesta:**
  - Cuando un sello o lienzo recibe exposición a agua (`Exposure.Wetness`), consultar si el hechizo activo o los trazos del sello fueron dibujados con tinta resistente (`spell.waterproof == true`).
  - Si es resistente, el agua no degrada la integridad de la tinta, no cancela el estado de emisión del `MagicField` ni despawnea la manifestación si el papel se sumerge en una masa de agua (`WaterVolume`).

### 4.2. Región (`region`) en Fuego, Agua, Viento y Luz
- **Módulo responsable:** `Output/SpellManifestation.cs` y `Output/MagicField.cs`.
- **Propuesta:**
  - **Fuego + Región:** En lugar de un chorro cónico, instanciar un emisor de partículas en anillo o línea rectangular (cortina/anillo de fuego) y restringir el `ExposureAt` a un volumen delimitado (cortafuegos).
  - **Agua + Región:** Manifestar una cortina laminar descendente o un área de lluvia circunscrita a una caja de influencia (*Rainbringer*), mojando únicamente lo que cruce esa frontera.
  - **Viento + Región:** Crear una barrera deflectora plana (muro de viento) que rechace proyectiles entrantes sin empujar fuera de sus límites geométricos.
  - **Luz + Región:** Crear una cúpula o perímetro de iluminación estricto con penumbra nula fuera del borde (pantalla luminosa).

### 4.3. Gravedad Escalar (`spell.gravity`)
- **Módulo responsable:** `Output/SpellManifestation.cs` y `Output/MagicField.cs`.
- **Propuesta:**
  - En `SpellManifestation`, aplicar `spell.gravity` al módulo de física de partículas (`gravityModifier = look.gravity * spell.gravity`), permitiendo que el chorro caiga en arco parabólico o flote suavemente según el grado de levitación.
  - En `MagicField`, aplicar una fuerza de flotabilidad pasiva proporcional a `(1 - spell.gravity)` a todos los cuerpos dentro del alcance (`ReachRadius`), simulando ingravidez progresiva sin requerir un orbe completo.

### 4.4. Apertura y Cono (`spread`) en Todos los Elementos y VFX
- **Módulo responsable:** `Output/SpellManifestation.cs` y `Output/MagicField.cs`.
- **Propuesta:**
  - En `SpellManifestation.cs:81`, hacer que el ángulo del cono del jet combine tanto la convergencia como la dispersión: `cone = Mathf.Clamp(coneBase * (1 + 1.8 * spell.spread) / plan.Focus, 2, 45)`.
  - En `MagicField.cs:104`, generalizar `JetRadiusAt` para que Fuego, Agua y Luz también expandan su radio de influencia axial con `coneSpread`, abriendo abanicos cónicos de llamas o aspersión de agua.

### 4.5. Feedback Visual y Sensorial de Signos en `SpellManifestation`
- **Módulo responsable:** `Output/SpellManifestation.cs` y shaders/VFX en `Prefabs/VFX/`.
- **Propuesta:**
  - **Crush:** Emitir partículas de impacto fracturado (fragmentos y ondas de choque angulares) en el punto de contacto.
  - **Orb:** Spawnear una esfera de malla procedural o billboard translúcido con shader Fresnel aditivo centrado en el punto del orbe, que se contraiga al vencer la duración.
  - **Billowing:** Modificar la tasa de emisión y tamaño del ParticleSystem para generar nubes voluminosas de baja velocidad y alto radio, con turbulencia aumentada.
  - **Cool:** Tinte cromático hacia azul/blanco mediante `MaterialPropertyBlock` y emisión secundaria de niebla o vaho frío cayendo hacia el suelo.
  - **Empower:** Escalar el multiplicador de emisión y el brillo HDR del material, sumando chispas energéticas periféricas.
  - **Focus (Mira):** Trazar una estela tenue o línea de partículas entre el sello y la posición del objetivo fijado (`Health`).
  - **Repetition:** Efecto de inversión de tiempo con partículas que se absorben hacia el sello y destello temporal.
  - **Weave:** Generar dos emisores gemelos con desfase orbital helicoidal en el módulo `velocityOverLifetime` para que las trayectorias de partículas se entrelacen visualmente en una trenza.
