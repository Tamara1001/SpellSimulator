# Plan: mapa del modo Libre ordenado por flujo de aprendizaje

> **Estado: APROBADO por Tamara (2026-11-16), pendiente de implementar.** Decisiones D1-D18 en §3 (las D15-D18 y §12b = **v2**, que pisa a v1).
> Nada de esto está implementado todavía: ningún paso del tracker de abajo está hecho; no se tocó código, escenas, prefabs, `.asset` ni `.meta`.
> La revisión crítica externa está en [MAPA_FLUJO_CRITICA.md](MAPA_FLUJO_CRITICA.md) y ya está volcada en §12b.

## 🚀 «Avanzá con el mapa» — protocolo para quien retome (persona o Claude)

Si Tamara o un compañero dice **«avanzá con el mapa»**, hacé esto, en este orden:

1. Leé `CLAUDE.md`, este documento completo (sobre todo §3, §11, §12b) y `Docs/HARNESS.md`. `git pull` y `git log --oneline -15` (la sesión de glifos toca `Data/Dictionary`, `Scripts/Compiler` y puede tocar `Data/Lore/glifos.json`).
2. Mirá el **tracker** de abajo: el primer paso sin tildar, respetando el orden v2, es el que sigue.
3. Hacé **solo ese paso** (o los que dependan solo de él), con su criterio de «listo» de §11. Los pasos de arte con **Opus** van a un subagente con especificación cerrada (tabla §14.4b); los subagentes **no compilan**: compilá y verificá vos.
4. Verificá: compilación limpia + tests EditMode + `HarnessPlaySmoke` (+ capturas en `Docs/Capturas/`).
5. Un commit chico en `main`, en español, con trailer `Co-Authored-By`. **No pushear salvo que Tamara lo pida.** No commitear ruido del Editor (`git checkout -- <archivo>`).
6. **Tildá el paso en el tracker**, anotá posiciones reales / sorpresas en «Bitácora» (abajo) y, si cambió una decisión, agregá una fila en §3. Actualizá `Docs/CHANGELOG.md` (y `KNOWN_ISSUES.md` si descubriste un bug).
7. Si dudás de diseño o de controles: **preguntale a Tamara antes** (ver CLAUDE.md). Rediseños nuevos fuera de este plan: media página y esperar el sí.

### Tracker (orden v2; tildar al cerrar cada paso con su commit)

- [ ] **0** Volcar posiciones reales (`Sites()`), alturas, `Path8/Path9`, inflamabilidad de pinos; capturas «antes»
- [ ] **1** Mover el laboratorio a la pradera (20,−8) — **incluye tocar `TrackClear`/frente al este** (§12b-1)
- [ ] **2** Mover el entrenamiento a (0,−26), frente O/SO; actualizar tests
- [ ] **2b** Caminos completos (`ValleyShape.Paths`) **antes** de fijar sitios 3-6
- [ ] **3** Archivo (rotonda abierta) en (0,−46) con 3 libros: `variants`, `motion`, `shape`
- [ ] **4** Campo de agua (orilla real vía `FindLakeside`, ≈ −22,−30)
- [ ] **5** Campo de tierra (20,−50) + santuario (44,−38) + peñascos 1 y 3 al este/norte
- [ ] **6** Campos de fuego (−18,−50) y viento (26,−34): aplanado, sin pinos en r=10, props
- [ ] **7** Reparto de libros de hechizos en `glifos.json` (releer el JSON actual: hay 7 hechizos nuevos de `b83b280`) + libro `seals`
- [ ] **8b** Portal Archivo ↔ torre (D17)
- [ ] **9** Carteles + mapa de la torre (grupo «Campos de elemento») + `LearningFlowTests`
- [ ] **9b** Libro «El círculo y la columna» en la torre (D16)
- [ ] **10** Casitas (libros de hechizos combinados; mientras tanto `spells-siege` a la cabaña del bosque) + camino torre→norte
- [ ] **13** Rework de la torre (A nichos, B estantes, C terraza) — subagente **Opus**
- [ ] **11** Estandartes de color por elemento (malla emisiva sin luz)
- [ ] **12** Docs finales (`GAME_DESIGN`, `STRUCTURE`, `ROADMAP`, `STATUS`, `KNOWN_ISSUES`, `CHANGELOG`) + prueba manual en PC y Quest

### Bitácora (anotar acá lo que se descubra al implementar; más reciente abajo)

- 2026-11-16 — Plan escrito y aprobado; crítica externa volcada (v2). Aún sin implementar.
> Autora del diseño: Tamara D'Angelo (decisiones) + sesión de diseño de Claude (redacción). Fecha: 2026-11-16.
> Relacionado: [MAPA_REORGANIZACION.md](MAPA_REORGANIZACION.md) (diagnóstico de distancias, previo), [GAME_DESIGN.md](GAME_DESIGN.md),
> [GLYPHS.md](GLYPHS.md), [ARCHITECTURE.md](ARCHITECTURE.md), [HARNESS.md](HARNESS.md).

## Cómo leer este documento (si sos el compañero que lo va a implementar)

1. §1-§3: **qué** queremos y por qué (decisiones ya tomadas).
2. §4-§6: **dónde** queda cada cosa (coordenadas, dibujo, distancias).
3. §7-§9: **qué se enseña** y cómo se guía al jugador (libros, caminos, progreso).
4. §10-§11: **qué archivos tocar** y **en qué orden** (pasos chicos, cada uno con su criterio de «listo»).
5. §12: riesgos. §13: lo que falta decidir.
6. §14: cómo trabajar desde tu PC (comandos, reglas del repo, qué delegar).

Todas las coordenadas son **metros en el mundo Unity: X = este, Z = norte** (`ValleyShape`). El spawn es (0, −6) y la torre (0, 16).
Los números de «hoy» salen de leer `ValleyShape.cs`, `MapStructures.cs`, `WorldZones.json` y `glifos.json` en el commit `265c873`; si el repo cambió, re-verificalos antes de tocar.

---

## 1. Objetivo en una frase

Que el jugador **entienda en qué orden aprender** (torre → entrenamiento → archivo de libros → un campo por elemento → casitas, atalaya y portales)
con un mapa **compacto y caminable**, **sin bloquear nada** (todo sigue accesible siempre) y con el **laboratorio fácil de encontrar**.

## 2. Qué pidió Tamara (resumen fiel)

- Se empieza en la **torre**: se conocen los 4 elementos y las columnas.
- Luego el **campo de entrenamiento**: runas, sellos y lienzos especiales.
- Luego un lugar con **más libros** (otros sigilos y modificadores/signos).
- Luego zonas de entrenamiento por elemento (agua, tierra, fuego, viento), cada una con **hechizos de su tipo** aprendibles.
- Lo último: **casitas, torre de vigilancia, portales** = late game, aprenden hechizos y cosas nuevas que **requieren la base**.
- El **laboratorio** no va en el flujo normal, pero tiene que ser **muy accesible y fácil de encontrar**.
- Aclaración importante: **los 4 elementos NO tienen orden obligatorio**. Cada jugador elige a cuál ir; por eso deben estar cerca entre sí.
  Se puede mostrar un orden *recomendado*, nunca obligatorio.

## 3. Decisiones tomadas en la entrevista (fuente de verdad)

| # | Tema | Decisión |
|---|---|---|
| D1 | Libros de glifos | Se **mueven**: la torre queda solo con *Los cuatro elementos*; los otros 4 libros de glifos van a una **biblioteca nueva, el «Archivo»**. |
| D2 | Zona de agua | Se **reusa** el Lago (con cascada) y el Laboratorio del lago como «Campo de agua»; solo se reubica la parte construida (el lab del lago) a la orilla sur. |
| D3 | Bloqueos | **Solo orienta**. Nada se bloquea. Guía = caminos + carteles + mapa de la torre con «siguiente etapa» + destello de color por elemento. |
| D4 | Laboratorio | **En la pradera, cerca del spawn** (a ~20 m, pista hacia el este), con cartel visible desde el spawn. Sin tocar su diseño (consola, 2 alas, pista de ~16 m). |
| D5 | Elementos | **Tierra** = taller de piedra reubicado; **Fuego** = zona **nueva**; **Viento** = campo **nuevo**; **Agua** = lago (D2). **Cualquier orden**. |
| D6 | Viento | Campo llano y abierto **dentro del anillo** (veletas/blancos colgantes). La **atalaya NO es el campo de viento**: sigue siendo late game. |
| D7 | Libros de hechizos | **Un libro de hechizos por zona de elemento**, repartiendo los actuales (hoy están mezclados). |
| D8 | Late game | Cristal en el santuario, asedio y portales de las alturas en la atalaya, portales del valle en el círculo, portal de la cueva. Casitas y cueva = «saber avanzado». |
| D9 | Disposición | **Anillo alrededor del Archivo, al sur**. Torre/norte = inicio y late game; el aprendizaje intermedio ocupa el sur. |
| D10 | Etapa 2 | **Libro nuevo + estaciones con cartel** en el campo de entrenamiento («Sellos, runas y lienzos»). |
| D11 | Casitas (Q1) | Libros de **hechizos que combinan elementos** (late game). Contenido exacto depende de la sesión de glifos; hasta entonces, refugio con mesa. |
| D12 | `rare` (Q2) | «Signos raros» **se queda en la terraza** de la torre (premio por subir). El Archivo tiene **3** libros: `variants`, `motion`, `shape`. |
| D13 | Torre | **Rework visual y de contenido** (§16), misma estructura de pisos; modelos nuevos con Opus. |
| D14 | Portal nuevo (Q4) | ~~No por ahora~~ → **REABIERTA y cambiada por D17.** |
| D15 | Coordenadas v2 | **Aceptadas** las de §12b (fuego −18,−50; tierra 20,−50; santuario 44,−38; agua en la orilla real; peñascos 1 y 3 al este/norte). |
| D16 | Columna | **Libro corto nuevo en la torre** «El círculo y la columna» (`column` se queda también donde esté en `shape`, o se mueve a ese libro: decide quien implemente, sin duplicar). |
| D17 | Portal Archivo ↔ torre | **Sí, ahora.** Un portal nuevo en el Archivo con vuelta al estudio de la torre (ver paso 8b en §11: receta, `PortalRecipeTests`, `PortalKnowledgeTests`, smoke de 5 portales). |
| D18 | Rework de la torre | Se hacen **A (nichos reactivos), B (estantes que se llenan) y C (terraza mirador)** de §12b. |

---

## 4. Estado actual (verificado en el repo)

### 4.1 Posiciones hoy (de `MAPA_REORGANIZACION.md`, `ValleyShape.cs`, `MapStructures.cs`)

Radio jugable 68 m (montañas desde ~64 m). Spawn (0,−6). Torre (0,16), loma r=20.

| Zona | Pos hoy (x, z) | Dist. a la torre |
|---|---|---|
| Entrenamiento | 16, −46 (`ValleyShape.TrainingOrigin`) | 64 |
| Laboratorio | 47, −27 (`MapStructures.LaboratoryPreferred`) | 64 |
| Santuario | 34, −36 | 62 |
| Círculo de piedras (portal `field-circle`) | 50, −10 | 56 |
| Pradera / campo viejo | 28, −6 (`ValleyShape.FieldCenter`, plano r=11, casi vacío) | 36 |
| Lago | −34, −16 | 47 |
| Lab. del lago | −44, 1 (`FindLakeside`) | 47 |
| Cabaña bosque | −28, 34 | 33 |
| Cabaña norte | −8, 46 | 31 |
| Puesto de suministros | 10, 48 | 34 |
| Cueva (portal) | −32, 52 | 48 |
| Taller de piedra | 40, 30 | 42 |
| Atalaya (portal) | ~37, 44 (calculada por `Sites()`) | 47 |
| Peñascos 1-4 (decorativos) | (20,−28) (44,10) (−6,−40) (22,30) | — |

Problemas (resumen del diagnóstico previo): entrenamiento y laboratorio a 64 m de la torre; el sureste está apiñado y el suroeste vacío;
ningún camino llega a las zonas de práctica; el aprendizaje no tiene un recorrido legible.

### 4.2 Dónde se enseña cada cosa hoy (verificado en `Data/Lore/glifos.json` y `Editor/Map/*`)

Glifos conocidos al empezar (`starting`): `magic_circle`, `fire`, `water`, `wind-directs-air`, `earth`.
(El 2.º dato de Tamara: «en la torre se conocen los 4 elementos»: ya es así por `starting`; el libro *elements* los explica.)

| Libro (`id`) | Título | Contiene | Dónde está hoy |
|---|---|---|---|
| `elements` | Los cuatro elementos | glifos fire, water, wind-directs-air, earth | Torre (planta baja, atril de la entrada) |
| `variants` | Variantes y parientes | light, crystal, aeroform, wind-underfoot | Torre (escritorio) |
| `motion` | Signos del movimiento | dispersion, pull, float, levitation | Torre (mesa del estudio) |
| `shape` | Dar forma a la magia | column, convergence, region, crush | Torre (mesa del estudio) |
| `rare` | Signos raros | collection, billowing, repetition, weave | Torre (pedestal de la terraza) |
| `spells-wind-water` | Hechizos de viento y agua | wind_fan, light_rain, whirl, updraft, wind_breath | Cabaña del norte |
| `spells-fire-earth` | Hechizos de fuego y tierra | small_flame, earth_spike, blaze, stone_wall | Cabaña del bosque |
| `spells-water-deep` | Aguas y mareas | downpour, wave, water_lance, water_draw | Círculo de piedras (`StoneCircle`) |
| `portals-valley` | Caminos del valle | portales `tower-study`, `field-circle` | Círculo de piedras |
| `spells-crystal` | Cristal y centelleo | crystal_shard, crystal_dome | Santuario |
| `spells-siege` | Arte de asedio | thrown_rock, fire_wall | Atalaya |
| `portals-heights` | Caminos de las alturas | portales `watchtower`, `cave` | Atalaya |

Los libros se colocan en código de editor: `MapDetails.cs` (los 5 de la torre, líneas ~280-284) y `MapStructures.cs` (`Cabin(...)`, `StandBook(...)`).
Cada libro es un `LibraryBook` (`Scripts/Learning/LibraryBook.cs`) con un `BookId` que busca en `glifos.json`.
**El significado de los glifos/libros es data (JSON)**: mover o repartir libros = editar `glifos.json` + el lugar donde se coloca el libro, sin tocar lógica.

> **Aviso de coordinación:** la otra sesión (glifos sin efecto) edita `Assets/_Project/Data/Dictionary/*` y `Scripts/Compiler/*`. Este plan **no toca** esos archivos.
> `glifos.json` está en `Data/Lore/`, **otra carpeta**, pero la sesión de glifos puede agregarle spells/glifos. Antes del paso 7, hacer `git pull`/revisar `git log -- Assets/_Project/Data/Lore/glifos.json`
> y mergear a mano si cambió.

---

## 5. Flujo de aprendizaje por etapas

```
ETAPA 1  Torre ─────────► ETAPA 2  Entrenamiento ───► ETAPA 3  Archivo ──► ETAPA 4  Campos de elemento (cualquier orden) ──► ETAPA 5  Late game
(inicio)                  sellos, runas, lienzos       más sigilos/signos    Agua · Tierra · Fuego · Viento                    casitas, atalaya, portales, cueva, santuario
                                                                                                                                  
            Laboratorio: fuera del flujo, siempre a mano en la pradera (junto al spawn)
```

| Etapa | Zona | Qué aprende | Libros / estaciones | Notas |
|---|---|---|---|---|
| 1 | **Torre** (0,16) | Los 4 elementos (ya conocidos al empezar), el círculo mágico y la columna; cómo leer y dibujar | Libro `elements` (planta baja). Papel emisor en la pared y en el escritorio | Se queda como hoy menos 4 libros de glifos. |
| 2 | **Campo de entrenamiento** | Sellos, runas (de piedra, de metal, de oro), lienzos especiales (estampas S/M/L, papel, atril), practicar con blancos y muñecos | Libro **nuevo** `seals` «Sellos, runas y lienzos» + estaciones con cartel (blancos, estampas, atril, papel, runas) | Reusa lo construido; se agregan el libro y los carteles. |
| 3 | **Archivo** (biblioteca 2, nueva) | Otros sigilos y modificadores/signos | Libros `variants`, `motion`, `shape`, `rare` (mudados de la torre) | Es el **centro del anillo**. `rare` se lleva su «premio por subir» (ver §12 riesgo R6). |
| 4a | **Campo de agua** (lago) | Hechizos de agua | Libro nuevo `spells-water` (de `spells-water-deep` + `light_rain`) + ejercicios del lab. del lago | Reusa Lago + cascada. |
| 4b | **Campo de tierra** (ex taller de piedra) | Rocas, muros, puentes, romper paredes | Libro nuevo `spells-earth` (earth_spike, stone_wall) | Se mueve el taller. |
| 4c | **Campo de fuego** (nuevo) | Llamas, hechizos de fuego, mundo inflamable | Libro nuevo `spells-fire` (small_flame, blaze) | Zona nueva (claro quemado). |
| 4d | **Campo de viento** (nuevo) | Corrientes, empujar cosas, flotar | Libro nuevo `spells-wind` (wind_fan, whirl, updraft, wind_breath) | Zona nueva (llano abierto). |
| 5 | **Late game**: casitas, atalaya, portales, cueva, santuario | Hechizos que **combinan** elementos y cosas que requieren la base | Siege + portals-heights (atalaya), portals-valley (círculo), crystal (santuario), **libros nuevos en las casitas (por definir, §13-Q1)** | Se queda en el norte/exterior. |
| — | **Laboratorio** | Probar cualquier sello sin límites | Sin libro | Pradera, a 20 m del spawn. |

Regla de oro: **ningún libro ni zona se bloquea.** El «orden» solo se sugiere (§9).

---

## 6. Disposición espacial acordada

### 6.1 Coordenadas objetivo

> Son **objetivos**: `MapStructures.Find(...)` puede correr un sitio unos metros si el terreno no es plano. Después de cada paso hay que
> volcar las posiciones reales (§11, paso 0) y comparar. Distancias de la tabla = línea recta entre centros.

| Zona | Objetivo (x, z) | Hoy | Cambio |
|---|---|---|---|
| Spawn | 0, −6 | igual | — |
| Torre | 0, 16 | igual | — |
| **Laboratorio** | **20, −8** (pradera, pista hacia el este, ~17 m libres) | 47, −27 | mover |
| **Entrenamiento** | **0, −26** | 16, −46 | mover (`ValleyShape.TrainingOrigin`) |
| **Archivo** (nuevo) | **0, −46** | — | crear |
| **Campo de agua** (lab. del lago) | **−26, −38** (orilla sur del lago) | −44, 1 | mover |
| **Campo de tierra** (ex taller) | **22, −54** | 40, 30 | mover |
| **Campo de fuego** (nuevo) | **−16, −58** | — | crear (r ≈ 60: cerca del borde, ver riesgo R3) |
| **Campo de viento** (nuevo) | **26, −34** | — | crear |
| Santuario | 46, −40 | 34, −36 | mover (hace lugar al viento) |
| Peñasco 1 | ~12, −40 | 20, −28 | mover (hace lugar al viento/entrenamiento) |
| Peñasco 3 | ~−10, −30 | −6, −40 | mover (hace lugar al Archivo) |
| Círculo de piedras (portal) | 50, −10 | igual | — |
| Lago / cascada | −34, −16 | igual | — |
| Cabaña bosque, cabaña norte, puesto, cueva, atalaya | igual | igual | — (late game) |
| Pradera (`FieldCenter`) | 28, −6 | igual | queda como pradera del laboratorio |
| Taller de piedra viejo (40,30) | — | 40, 30 | **se libera** (queda terreno vacío; el peñasco 4 en 22,30 se mantiene) |

### 6.2 Dibujo (vista desde arriba, norte arriba, 4 m por columna, 8 m por fila, generado con las coordenadas de arriba)

```
           ...       ...
       ..U                ..          T  Torre            S  Spawn       L  Laboratorio
     ..        n    p     A ..        E  Entrenamiento    B  Archivo
    .     b                   .       W  Campo de agua    ~  Lago
   .                           .      R  Campo de tierra  F  Campo de fuego   V  Campo de viento
  .                             .     H  Santuario        C  Círculo (portal)
 .               T               .    A  Atalaya (portal) U  Cueva (portal)
 .                               .    n  Cabaña norte     p  Puesto    b  Cabaña bosque
 .               S               .
 .                    L       C  .
  .      ~                      .
   .             E             .
    .      W            V     .
     ..          B          .H
       ..              R  ..
           ..F       ...
```

Lectura: el **norte** (n, p, b, U, A) es late game y exploración; el **centro** es la torre y el spawn con el laboratorio al lado;
el **sur** es el anillo de aprendizaje (E → B → W/R/F/V).

### 6.3 Distancias (línea recta, metros; correr ≈ 5 m/s, caminar ≈ 1,4 m/s; en VR la meta es ≤ 30 m entre estaciones consecutivas)

| Recorrido | Metros | Correr | Meta |
|---|---|---|---|
| Spawn → Torre | 22 | 4 s | ok |
| Spawn → Laboratorio | 20 | 4 s | ok (accesibilidad) |
| Spawn → Entrenamiento | 20 | 4 s | ok |
| Torre → Entrenamiento | 42 | 8 s | ok (antes 64) |
| Entrenamiento → Archivo | 20 | 4 s | ok |
| Archivo → Agua | 27 | 5 s | ok |
| Archivo → Tierra | 23 | 5 s | ok |
| Archivo → Fuego | 20 | 4 s | ok |
| Archivo → Viento | 29 | 6 s | ok |
| Agua ↔ Fuego | 22 | 4 s | ok (elegir cualquiera) |
| Tierra ↔ Viento | 20 | 4 s | ok |
| Fuego ↔ Tierra | 38 | 8 s | aceptable (lados opuestos del anillo; el Archivo en medio) |
| Archivo → cualquier campo | 20-29 | 4-6 s | **todos a ≤ 30 m** (pedido de Tamara: «no demasiado lejos») |
| Spawn → Fuego (el más lejano) | 54 | 11 s | aceptable; hay camino (§8) |

Comparado con el diagnóstico previo: antes el salto aprender→practicar era de 91 m; ahora el peor es 42 m.

### 6.4 Qué pasa con el laboratorio (D4)

- Sitio nuevo ≈ (20, −8) sobre la pradera plana (`FieldCenter` 28,−6, r=11), **a 20 m del spawn** y a la vista del camino. Pista hacia el **este** (hacia el círculo), de 12 × 10 m + ~5 m de plataforma; `LaboratoryFront` ya elige el frente más despejado, así que no hay que tocar su código, solo `LaboratoryPreferred`.
- Cartel de madera **junto al spawn** («Laboratorio →») y destello de color azul (el del laboratorio en `WorldZones.json`: `#4F7FC4`).
- La **consola, las dos alas, la pista de ~16 m y las estacas** no se modifican (ver [GAME_DESIGN.md → Laboratorio](GAME_DESIGN.md#laboratorio-y-lentes-de-tinta)).
- La fogata decorativa va «lejos» del pasto de la pista (`MinFireDistance` 8 m): revisar después de mover.
- El `StampArea` del campo viejo (28, −16.5) queda libre; no choca con el laboratorio nuevo (distancia ~12 m).
- El **entrenamiento viejo en el campo** (`FieldCenter`) no existe (ya se movió a 16,−46); la pradera queda **solo** con el laboratorio.

---

## 7. Libros: antes y después

| Libro | Hoy | Después | Cambio en `glifos.json` |
|---|---|---|---|
| `elements` | Torre | Torre (igual) | ninguno |
| `variants`, `motion`, `shape`, `rare` | Torre | **Archivo** | ninguno de contenido; cambia el lugar donde se coloca (`MapDetails`/estructura nueva) |
| `seals` (**nuevo**) «Sellos, runas y lienzos» | — | Campo de entrenamiento | libro nuevo, `glyphs: []`, sin hechizos; solo texto (ver abajo) |
| `spells-wind-water` | Cabaña norte | **se parte** en `spells-wind` y `spells-water` | borrar y crear 2 |
| `spells-fire-earth` | Cabaña bosque | **se parte** en `spells-fire` y `spells-earth` | borrar y crear 2 |
| `spells-water-deep` | Círculo | **se une** a `spells-water` | los hechizos `downpour, wave, water_lance, water_draw` pasan a `spells-water` |
| `spells-water` (nuevo) | — | Campo de agua | `light_rain, downpour, wave, water_lance, water_draw` |
| `spells-wind` (nuevo) | — | Campo de viento | `wind_fan, whirl, updraft, wind_breath` |
| `spells-fire` (nuevo) | — | Campo de fuego | `small_flame, blaze` (+ lo que agregue la sesión de glifos) |
| `spells-earth` (nuevo) | — | Campo de tierra | `earth_spike, stone_wall` |
| `spells-crystal` | Santuario | Santuario (igual) | ninguno |
| `spells-siege`, `portals-heights` | Atalaya | Atalaya (igual) | ninguno |
| `portals-valley` | Círculo | Círculo (igual) | ninguno |
| **Casitas** (cabaña norte y cabaña bosque) | libros de hechizos | **libros avanzados nuevos (por definir)** | ver §13-Q1 |

Notas:
- Los nombres exactos de los libros nuevos (`title`, `color`) los decide quien implemente, con el mismo estilo de los existentes. El **orden de `books` en `glifos.json` define el número** («Libro 3 de 14»); mantener el orden pedagógico: elements, seals, variants, motion, shape, rare, spells-water, spells-earth, spells-fire, spells-wind, spells-crystal, spells-siege, portals-valley, portals-heights, (libros de casitas).
- **Compatibilidad de guardado:** el progreso guarda libros leídos por `id` (`DiscoveryState`). Borrar un `id` viejo (`spells-wind-water`, `spells-fire-earth`, `spells-water-deep`) hace que el jugador que ya los había leído los vea «sin leer» en los nuevos. Es aceptable (proyecto sin jugadores), pero verificarlo con `DiscoveryTests` (§10).
- Los **textos** del libro `seals` y los carteles los puede redactar una IA barata (ver §14.4) y revisar Tamara; usar el tono de `GlyphLore`.

---

## 8. Caminos y señalización (D3: solo orienta)

### 8.1 Caminos de tierra (`ValleyShape.Paths`)

Hoy son 3 tramos cortos de ~20 m. Agregar (puntos aproximados; ajustar a los sitios reales):

1. Spawn (0,−6) → Entrenamiento (0,−26) → Archivo (0,−46) — **la columna vertebral**.
2. Archivo → Agua (−26,−38); Archivo → Fuego (−16,−58); Archivo → Tierra (22,−54); Archivo → Viento (26,−34) — **cuatro ramas** a cada campo.
3. Spawn → Laboratorio (20,−8).
4. Torre → casitas/puesto (norte) y Torre → atalaya: caminos al late game (más tarde, paso opcional).

### 8.2 Carteles de madera

- Un cartel en el **spawn** con 3 flechas: Torre / Laboratorio / Entrenamiento.
- Cartel en cada **bifurcación** del Archivo: «Agua», «Tierra», «Fuego», «Viento» + una línea de «qué se aprende».
- Cartel por zona (nombre + qué hay). Reusar el mecanismo de carteles que ya tiene el laboratorio (cartel de madera de cara a la torre, `MapLab`); **verificar cómo se construye antes de inventar uno nuevo**.

### 8.3 Destellos de color por elemento (visibles de lejos)

Agua azul, Tierra marrón, Fuego rojo-naranja, Viento verde-claro; el laboratorio azul `#4F7FC4`. Una luz tenue o columna de partículas baja en cada campo, **sin allocations en `Update`** (CODESTYLE). Si cuesta, dejarlo como último paso opcional.

---

## 9. Cómo se ve el progreso (orientativo)

- **Mapa de la torre** (`MapTowerMap`, ver `TowerMapTests`): hoy lista 14 zonas con marcadores. Pasa a ~18: se suman Archivo, campo de fuego, campo de viento, entrenamiento (si no estaba) y se reubican los demás. El marcador de la **«siguiente etapa sugerida»** se resalta (borde/pulso) según qué libros leyó el jugador:
  - Nada leído → «Torre».
  - Leyó `elements` → «Entrenamiento».
  - Leyó `seals` → «Archivo».
  - Leyó ≥ 1 libro del Archivo → «Campos de elemento» (se marcan los 4; sin orden).
  - Leyó ≥ 1 libro de hechizos de elemento → «Late game».
  Es **solo un resaltado**; el jugador puede ir donde quiera. (Se calcula con `Discovery`/`DiscoveryState`, ya existentes.)
- **HUD de libro:** ya dice «Libro X de Y: título (faltan N por leer)». Se mantiene y se actualiza solo (usa `Lore.Books.Count`).
- **Sin bloqueos**, sin puertas. Los portales siguen sin requisitos nuevos en esta tanda.

---

## 10. Archivos a tocar y tests

> Reglas del repo: [CLAUDE.md](../CLAUDE.md). **No editar a mano `.unity`, `.prefab`, `.asset`, `.meta`.** El mundo se **regenera** con `Editor/Map/MapBuilder.Build()`; `MapLayout` conserva los ajustes manuales en `Data/MapOverrides.json` (no borrar ese archivo).

| Archivo | Qué cambiar |
|---|---|
| `Assets/_Project/Scripts/World/Map/ValleyShape.cs` | `TrainingOrigin` → (0,−26) y revisar `TrainingForward` (frente hacia el sur/Archivo); agregar los tramos de §8.1 a `Paths`. |
| `Assets/_Project/Editor/Map/MapStructures.cs` | `Sites()`: nuevo `LaboratoryPreferred` (20,−8); nuevas entradas `Archive`, `FireField`, `WindField`; nuevas semillas de `EarthWorkshop` (22,−54), `WaterLab` (en vez de `FindLakeside` → orilla sur ≈ (−26,−38)), `Shrine` (46,−40), `Crag_1`, `Crag_3`; métodos de construcción `Archive(...)`, `FireField(...)`, `WindField(...)`; mover `StandBook(...)` de los libros a sus zonas; la casa de los libros de las casitas. |
| `Assets/_Project/Editor/Map/MapDetails.cs` | Quitar de la torre los libros `variants`, `motion`, `shape`, `rare` (líneas ~281-284); dejar `elements`. |
| `Assets/_Project/Editor/Map/MapBuilder.cs` | Probablemente nada; si usa posiciones fijas del entrenamiento/laboratorio, actualizar. |
| `Assets/_Project/Data/Lore/glifos.json` | Libros nuevos y repartos de §7. **Datos, no código.** |
| `Assets/_Project/Data/WorldZones.json` | Agregar zonas `archive`, `fire-field`, `wind-field`; actualizar `use` de `training`, `water-lab`, `earth-workshop`, `laboratory`; las x/z de este JSON son **referencia** (se pisan al construir con la posición resuelta). |
| `Assets/_Project/Scripts/Learning/GrimoireTexts.cs` y `PlayerInputReader` | **No cambian** (los controles no se tocan en esta tanda). |
| `Docs/GAME_DESIGN.md` («Mapa del modo Libre», «Aprendizaje (M6)», «Laboratorio…») | Reflejar el nuevo mapa y el nuevo reparto de libros. |
| `Docs/STRUCTURE.md`, `Docs/CHANGELOG.md`, `Docs/ROADMAP.md`, `Docs/STATUS.md`, `Docs/KNOWN_ISSUES.md` | Según la tabla «Qué doc actualizar» de `CLAUDE.md`. |

### Tests y smoke que se rompen o hay que sumar (de `MAPA_REORGANIZACION.md` §4, a verificar al implementar)

| Test | Qué pasa |
|---|---|
| `StructureSitesPinTests` | Lista `Pinned` con las posiciones actuales (Laboratory 47,−27; EarthWorkshop 40,30; …): **actualizar** a las nuevas + sumar `Archive`, `FireField`, `WindField`; chequeos del frente del laboratorio y de `TrainingKeepOut`. |
| `TowerMapTests` | «los marcadores no se superponen» (≥ 2×`MarkerRadius`) y «cae dentro del cuadro»: **revisar**; con ~18 marcadores y varios en el sur puede fallar (R4). |
| `TrainingLayoutTests` | Solo usa `LegacyStation`: no debería cambiar. |
| `ValleyShapeTests` | Spawn y pradera libres de árboles: siguen valiendo; **agregar** que el Archivo y los campos no pisen peñascos ni árboles. |
| `DiscoveryTests`, `PortalKnowledgeTests` | Revisar los ids de libros viejos si los usan. |
| `HarnessPlaySmoke` | Mantener «4 portales, 3+ puntos de reaparición, estación de reciclaje»; **sumar** un chequeo por sistema nuevo: cada libro tiene su `LibraryBook` en su zona; el Archivo contiene 4 libros; el laboratorio sigue encontrando su pista. |
| **Test nuevo** (EditMode, lógica pura) | `LearningFlowTests`: dado el conjunto de libros leídos, qué etapa se sugiere (§9). Lógica en una clase estática sin `MonoBehaviour` (regla del repo). |

---

## 11. Pasos de implementación (commits chicos en `main`, sin push)

Convención: **[REDISEÑO]** = cambia diseño/estructura, pide compilar+smoke; **[BUG]** = corrige algo existente. Hoy casi todo es REDISEÑO; los BUG potenciales aparecen al probar y se anotan en `KNOWN_ISSUES.md`.
Cada paso: compilar limpio → tests EditMode verdes → `HarnessPlaySmoke` (ver `Docs/HARNESS.md`), **un commit en español**, trailer `Co-Authored-By`, **sin push**, **sin commitear ruido del Editor** (`git checkout -- <archivo>` para `*_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings`, `OpenXRPackageSettings`, `SpellSimulator.slnx`, defines de `ProjectSettings.asset`).

| # | Tipo | Paso | Criterio de «listo» |
|---|---|---|---|
| 0 | prep | **Volcar las posiciones reales** de todos los sitios (`Sites()`) a un archivo de texto en `Docs/` o consola, y guardar capturas «antes» del mapa de la torre. Pausar el auto-refresh si se mueven `.cs` (ver `HARNESS.md`). | Tabla «hoy» confirmada; capturas en `Docs/Capturas/`. |
| 1 | REDISEÑO | **Mover el laboratorio** a la pradera: `LaboratoryPreferred` = (20,−8). Regenerar mapa. Actualizar `StructureSitesPinTests`. Revisar pista y fogata. | Lab a ≤ 22 m del spawn, pista de ~17 m despejada, tests verdes, captura. |
| 2 | REDISEÑO | **Mover el entrenamiento**: `TrainingOrigin` = (0,−26) y `TrainingForward`; mover peñascos 1 y 3; actualizar tests de entrenamiento. | Entrenamiento a 20 m del spawn; blancos/atril funcionan; sin árboles encima. |
| 3 | REDISEÑO | **Crear el Archivo** (edificio chico con estantes: 4 libros) en (0,−46) y **mover los 4 libros de la torre** (`MapDetails` + nueva estructura). `glifos.json` no cambia aún. | Los 4 libros se leen en el Archivo; la torre solo tiene `elements`; HUD «Libro N de 12». |
| 4 | REDISEÑO | **Campo de agua**: mover `WaterLab` a la orilla sur (−26,−38); ajustar el camino. | Ejercicios de agua funcionan, la caja se empuja al lago. |
| 5 | REDISEÑO | **Campo de tierra**: mover el taller a (22,−54); mover el santuario a (46,−40) si choca. | Rocas/muros/puentes funcionan; peñasco libre. |
| 6 | REDISEÑO | **Campos de fuego y viento** (nuevos, mínimos viables: terreno plano + 2-3 objetos de práctica + cartel). | Cada uno tiene su cartel y al menos un blanco/ejercicio; smoke en verde. |
| 7 | REDISEÑO (datos) | **Repartir libros de hechizos** (§7) en `glifos.json` y colocarlos en cada campo; crear `seals` y ponerlo en el entrenamiento. Revisar `git log` del JSON por cambios de la otra sesión. | `DiscoveryTests` y smoke verdes; el HUD muestra «Libro N de Y» coherente. |
| 8 | REDISEÑO | **Caminos** (§8.1) en `ValleyShape.Paths` + `ValleyShapeTests`. | Los caminos se ven en el suelo; sin árboles encima. |
| 9 | REDISEÑO | **Carteles** (§8.2) y **mapa de la torre**: marcadores nuevos + resaltado de «siguiente etapa» (§9) + `LearningFlowTests`. | `TowerMapTests` verdes; captura del mapa. |
| 10 | REDISEÑO | **Casitas** con libros avanzados (según §13-Q1) y revisión del late game. | Cada casita tiene libro; smoke verde. |
| 11 | opcional | **Destellos** de color por elemento (§8.3). | Sin allocations en `Update`. |
| 2b | REDISEÑO | **(v2) Caminos primero**: definir `ValleyShape.Paths` completos (columna vertebral, ramas que terminan en el borde de cada campo, arco agua→fuego→tierra→viento, torre→norte) **antes** de fijar los sitios 3-6; actualizar `ValleyShapeTests`. Reemplaza al paso 8. | Los sitios nuevos pasan `Clear`; árboles fuera de los caminos. |
| 8b | REDISEÑO | **(v2) Portal Archivo ↔ torre** (D17): nuevo portal en el Archivo (id sugerido `archive`), receta en el diccionario de portales, libro `portals-valley` actualizado; `PortalRecipeTests`, `PortalKnowledgeTests`, smoke «5 portales». **No tocar** `Data/Dictionary` de glifos (otra sesión); si hace falta una receta nueva, coordinar. | Cruzar el portal lleva a la torre y vuelta. |
| 9b | REDISEÑO | **(v2) Libro de la torre «El círculo y la columna»** (D16) y corrección de `starting`/libros. | La torre enseña círculo y columna. |
| 13 | REDISEÑO | **Rework de la torre** (§16; ideas A+B+C de §12b), después del paso 9; subagente Opus con especificación cerrada. | Capturas antes/después, rampas y portal intactos, smoke verde. |
| 12 | docs | Actualizar `GAME_DESIGN.md`, `STRUCTURE.md`, `ROADMAP.md`, `STATUS.md`, `KNOWN_ISSUES.md`, `CHANGELOG.md`; probar a mano en PC y en Quest. | Docs al día; lista de pruebas manuales anotada. |

Dependencias: 0 → (1, 2 en cualquier orden) → 3 → (4, 5, 6 en paralelo si tocan archivos distintos) → 7 → 8 → 9 → 10 → 12. Los pasos **1** y **2** son los de mayor beneficio inmediato (acortan el recorrido); si hay poco tiempo, parar ahí y está mejor que antes.

---

## 12. Riesgos

| ID | Riesgo | Mitigación |
|---|---|---|
| R1 | `Find()` corre un sitio unos metros y las distancias de §6.3 cambian | Paso 0 y re-medir tras cada paso; las metas son holgadas (≤ 30 m). |
| R2 | `.unity` regenerado genera un diff enorme en `Pradera.unity` | No editar a mano; revisar solo que el mundo se vea bien; commitear la escena junto con el cambio de `MapStructures`. |
| R3 | El campo de fuego en (−16,−58) está a r ≈ 60 m, junto a las montañas (pendiente) | Si no hay terreno plano, subirlo a (−18,−52) o aplanarlo en `ValleyShape.Height` (hay precedente con `TrainingFlatRadius`). |
| R4 | `TowerMapTests`: más marcadores en el sur → superposición | Subir resolución del mapa o achicar `MarkerRadius` o agrupar etiquetas; decidir en el paso 9. |
| R5 | La otra sesión toca `glifos.json` y hay conflicto | Mergear a mano en el paso 7; no tocar `Data/Dictionary/*` ni `Scripts/Compiler/*`. |
| R6 | `rare` hoy está en la terraza como «premio por subir» | Al mudarlo al Archivo se pierde ese premio. Opción: dejarlo en la torre (decisión §13-Q2). |
| R7 | Laboratorio recién ajustado v2-v6 (mucha UI) | Solo cambia el **sitio**, no el código; probar con la captura y el smoke del laboratorio. |
| R8 | Archivo/campos nuevos aumentan triángulos/draw calls en Quest | Mantenerlos low-poly, reusar materiales; medir con el profiler al final (paso 12). |
| R9 | Los guardados de jugadores con libros ya leídos (`spells-wind-water` etc.) | Proyecto académico sin jugadores en producción; si importa, migrar ids en `DiscoveryState`. |
| R10 | Reubicar el santuario/peñascos rompe tests que los fijan (`StructureSitesPinTests`) | Actualizar la lista `Pinned` en el mismo commit. |

---

## 12b. Correcciones tras la crítica externa (v2, ACEPTADAS por Tamara el 2026-11-16: D15-D18 en §3)

> Orden de ejecución v2 resumido: 0 → 1 → 2 → **2b (caminos)** → 3 → 4 → 5 → 6 → 7 → **8b (portal)** → 9 → **9b (libro torre)** → 10 → 13 → 11 → 12. El paso 8 original (caminos) queda absorbido por 2b.

Fuente: informe del subagente Fable 5.2 ([MAPA_FLUJO_CRITICA.md](MAPA_FLUJO_CRITICA.md); revisó el código, sin correr Unity). **Esta sección pisa a §6, §7 y §12 donde se contradigan.**
Todo lo marcado «a medir» hay que confirmarlo en el paso 0 con Unity.

### Coordenadas v2 (reemplazan las de §6.1 donde difieran)

| Zona | v1 (§6.1) | **v2** | Motivo |
|---|---|---|---|
| Campo de fuego | −16, −58 | **−18, −50** | Con z < −54,25 el marcador cae fuera del mapa de la torre (`TowerMapTests`); y `Clear` exige r + radio ≤ 63. |
| Campo de tierra | 22, −54 | **20, −50** | Mismo motivo (pasaba el cuadro por 2 mm). |
| Santuario | 46, −40 | **44, −38** | Con r = 61 + 3,8 falla el tope de `Clear`. |
| Campo de agua | −26, −38 | **orilla sur real vía `FindLakeside` con ángulo 300° (≈ −22, −30)** | (−26,−38) queda a 12,5 m del agua: la caja ya no se empuja al lago. Sigue a ~27 m del Archivo. |
| Peñasco 1 | ~12, −40 | **al sitio viejo del taller (40, 30)** | Caía sobre el camino Archivo–Viento. |
| Peñasco 3 | ~−10, −30 | **al sitio viejo del laboratorio (47, −27)** | Pisaba el aplanado del entrenamiento; de paso llenan el norte/este. |
| Entrenamiento: frente | sur | **oeste/suroeste**, camino pasando por detrás del atril | Con frente al sur, el muñeco A queda a 4-5 m de la pared del Archivo. |

### Problemas que cambian el plan (resumen; detalle en [MAPA_FLUJO_CRITICA.md](MAPA_FLUJO_CRITICA.md))

1. **El laboratorio SÍ requiere tocar código** (corrige §6.4): en (20,−8) `TrackClear` descarta pistas a < 13 m de `FieldCenter`, a < 5 m de `StampArea` y sobre caminos; el frente saldría al sur/suroeste. Acción (paso 1): quitar la regla de `FieldCenter` para el lab **o fijar el frente al este a mano**, y recortar el camino viejo cerca de (12,−7); revisar las prácticas `Practice/*_Path8/_Path9` de `MapOverrides.json` (posiciones absolutas, no se mueven con el mapa).
2. **Los caminos se definen ANTES que los sitios** (paso 8 pasa a **paso 2**): `Clear` exige > 3,2 m + radio de todo camino y `CanPlantTree` cambia todo el bosque; si no, se rompen los `Pinned` otra vez. Las ramas **terminan en el borde** de cada campo, no en el centro.
3. **La columna se enseñaba en la torre** pero `column` está en `shape` (que va al Archivo). Acción: libro corto en la torre «El círculo y la columna» **o** mover `column` a `elements`. *(Decisión pedida a Tamara.)*
4. **§7 está vieja:** el commit `b83b280` agregó 7 hechizos (hoy fuego 5, viento 6, agua 6, tierra 3). Rehacer el reparto leyendo `glifos.json` actual en el paso 7; tierra queda flaca: sumarle `thrown_rock` (hoy en `spells-siege`).
5. **Anillo sin arcos:** solo hay ramas radiales (~200 m ida y vuelta para ver los 4 campos; fuego↔tierra 38 m sin camino). Acción: **arco de camino agua→fuego→tierra→viento**. Late game queda a 80-110 m (fuego→cueva 111 m): ver pregunta sobre el portal Archivo↔estudio.
6. **Norte muerto:** casitas sin libro, taller liberado, sin caminos. Acción: no vaciar las casitas hasta tener sus libros nuevos (mientras tanto `spells-siege` pasa a la cabaña del bosque), y **hacer el camino torre→norte en esta tanda**. Los peñascos 1 y 3 también llenan ese lado.
7. **El laboratorio roba el arranque a la torre** (radio 8, dos alas, pista de 16 m, a 20 m del spawn): cartel chico y **sin destello fuerte** (§8.3 se cambia: el destello del arranque es el de la torre); capturar desde el spawn.
8. **Fuego y viento pobres:** darles aplanado (como `TrainingFlatRadius`) y **despeje de pinos** en r = 10 (`CanPlantTree` no conoce los sitios). Fuego: piso de ceniza sin pasto + anillo de piedras (cuidado: ¿los pinos son inflamables?, a medir). Viento: 3 veletas, mangas, ropa tendida.
9. **Mapa de la torre:** ya tiene 18 zonas, no 14 (pasa a 21-22). Sin superposición, pero la leyenda baja a ~3,4 cm por fila: **agrupar los 4 campos en una fila «Campos de elemento» con 4 chips de color.**
10. **Quest:** destellos como **estandartes de malla emisiva sin luz** (no columnas de partículas ni luces extra); Archivo como **rotonda abierta** (menos colliders, más cómodo en VR). Cambia §8.3 y paso 11.

### Ideas para el rework de la torre (§16, a elegir)

- **A. Aula de los cuatro elementos:** junto al atril de `elements`, 4 nichos reactivos (brasero, fuente, campanas de viento, maceta de piedra), cada uno se enciende al lanzar su signo básico; con los 4 encendidos se ilumina el marcador «Entrenamiento» en el mapa. Reusa `Map/Tower/Stations` y `SpellCaster.SpellCompiled`. Cierra bien la etapa 1.
- **B. Estantes que se llenan:** la biblioteca arranca vacía («los libros se repartieron por el valle») y cada libro leído afuera aparece como lomo de color (≈ 15 mallas estáticas activadas por `DiscoveryState`, sin `Update`). La torre = lugar al que se vuelve a ver el progreso.
- **C. Terraza mirador:** `rare` arriba + rosa de los vientos grabada en el piso con flechas de color a cada campo; los estandartes de los campos se ven desde ahí.

---

## 13. Aprobación y preguntas abiertas

> **Q1, Q2 y Q4 ya están respondidas** (D11, D12, D14). Donde dicen «Archivo con 4 libros» leer **3** (`rare` queda en la terraza). Q3 sigue abierta (propuesta: 1 libro de 3-4 páginas).

**Preguntas originales (historial):**

- **Q1.** ¿Qué enseñan los libros de las casitas (late game)? Hoy ya no tendrían libro. Propuesta: libros de **hechizos que combinan elementos** (ej.: fuego+viento, agua+tierra), que dependerán de qué hechizos nuevos agregue la sesión de glifos. *Hasta entonces las casitas quedan como refugio con mesa.*
- **Q2.** ¿`rare` («Signos raros», premio por subir a la terraza) se queda en la torre o va al Archivo? Propuesta: **se queda en la terraza** (conserva el «premio»), el Archivo tiene 3 libros.
- **Q3.** ¿Cuántos libros nuevos y qué texto para `seals`? Propuesta: 1 libro (3-4 páginas) escrito por IA barata y revisado por Tamara.
- **Q4.** ¿Se agrega un portal al anillo sur (atajo spawn ↔ Archivo)? Propuesta: **no en esta tanda** (el anillo ya es corto); evaluar tras probar a mano.

**Aprobación:** para implementar, Tamara tiene que responder en el chat (o editar acá) algo como *«Aprobado, empezá por el paso N»*. Hasta entonces no se escribe código.

---

## 14. Cómo trabajar esto desde tu PC

### 14.1 Preparación

```bash
git pull                               # o git fetch + revisar si la otra sesión cambió Data/Lore/glifos.json
git log --oneline -15
git status                             # ¡no commitear ruido del Editor!
```

Leer en este orden: `CLAUDE.md` → `Docs/ONBOARDING.md` → `Docs/STATUS.md` → este plan → `Docs/MAPA_REORGANIZACION.md` → `Docs/HARNESS.md` (trucos del MCP de Unity: parámetros `Code`/`Title`, esperar compilaciones, `delayCall` para builds, candado `Logs/unity.lock`).

### 14.2 Regenerar el mundo y verificar

- El mundo sale de `Editor/Map/MapBuilder.Build()`; los ajustes manuales viven en `Data/MapOverrides.json` (**no borrar**).
- Verificar con el harness (no a ojo): compilación limpia + tests EditMode + `HarnessPlaySmoke`. Capturas con `HarnessCapture` o `ScreenCapture`.
- Si hay que mover o renombrar `.cs`, mover también su `.meta` y **pausar el auto-refresh** primero (`AssetDatabase.DisallowAutoRefresh()`).

### 14.3 Reglas del repo que más importan acá

- Solo se edita código propio en `Assets/_Project/`. No tocar `Assets/Samples/`, `Packages/`, `Library/`.
- Nunca editar a mano `.unity`, `.prefab`, `.asset`, `.meta`.
- Significado de glifos = data (JSON). Lógica pura = clase estática + test EditMode. **Cero allocations en `Update/FixedUpdate/LateUpdate`.**
- Toda feature debe funcionar en **PC y VR** (solo vía `PlatformDetector.IsVR`).
- Todo en español (docs, commits, logs, comentarios); identificadores en inglés. **Push solo si se pide.**

### 14.4 Qué se puede delegar (ahorra cuota de Claude; ver `D:\Archivos\Claude\GUIA_SUBAGENTES.md`)

| Tarea | A quién | Cómo | Ojo |
|---|---|---|---|
| Redactar el libro `seals`, carteles y textos de libros nuevos (español, tono de `GlyphLore`) | MCP `obrero` (`generar`) | `mcp__obrero__generar` con el contexto de `glifos.json` | Verificar; no pasar secrets. |
| Repartir libros en `glifos.json` (paso 7) y escribir `LearningFlowTests` | `agy-jaula` (Antigravity) | `node D:/Archivos/Claude/agy-jaula/agy-jaula.mjs --proyecto D:\Programas\Unity\SpellSimulator --prompt "…"` (leer su `CLAUDE.md` antes) | Solo repos Git en `D:\Archivos`: **este repo está en `D:\Programas`**, hay que ver si lo admite; si no, delegar solo el texto/JSON a `obrero` y aplicarlo a mano. No compilan Unity: compila quien integra. |
| Segunda opinión del plan / revisar un diff | Codex | `codex exec --sandbox read-only "<pregunta>"` | Cuota chica, ocasional. |
| Compilar, correr tests, regenerar el mapa, capturas | Quien tenga el Unity MCP abierto | `HARNESS.md` | Un solo agente a la vez con Unity (candado `Logs/unity.lock`). |

### 14.4b Qué subagente y qué modelo para cada paso (decisión de Tamara: modelos nuevos/rehacer = Opus)

| Paso / tarea | Agente | Modelo | Por qué |
|---|---|---|---|
| Crítica del plan (hecha, antes del paso 0) | subagente Claude | Fable 5.2, esfuerzo medio | Barato y con criterio |
| 0 Volcado de posiciones, capturas «antes» | sesión principal (Unity MCP) | el de la sesión | Necesita el Editor |
| 1-2 Mover laboratorio y entrenamiento (constantes + tests) | sesión principal | Sonnet | Cambios puntuales, verificar con harness |
| 3 Archivo (edificio nuevo, modelo low-poly) | subagente Claude | **Opus** | Estética 3D / estructura nueva |
| 4-5 Mover agua y tierra | sesión principal | Sonnet | Solo posiciones y ajustes |
| 6 Campos de fuego y viento (props, terreno, ambiente) | subagente Claude | **Opus** | Diseño visual desde cero |
| 7 Reparto de libros en `glifos.json` | `agy-jaula` o sesión principal | Sonnet / Antigravity | JSON con especificación cerrada |
| Textos de libros y carteles | MCP `obrero` (`generar`) | DeepSeek (externo) | Barato; revisa Tamara |
| 8 Caminos (`Paths`) y tests | sesión principal | Sonnet | Lógica chica |
| 9 Carteles y mapa de la torre + `LearningFlowTests` | sesión principal (lógica) + Opus (arte del cartel) | Sonnet / Opus | Lógica ≠ estética |
| 10 Casitas | sesión principal | Sonnet | Reusa lo que hay |
| 13 Rework de la torre (modelos/estética) | subagente Claude | **Opus** | Rehacer modelos |
| 11 Destellos | subagente Claude | Sonnet | Partículas/luces con spec cerrada |
| Compilar, tests, regenerar mapa, capturas | sesión principal | — | Los subagentes **no compilan** |
| Segunda opinión de diffs grandes | Codex (`codex exec --sandbox read-only`) | — | Ocasional |
| Búsquedas/lecturas amplias | `explorador` o externos | Haiku / externo | Solo la conclusión |

Reglas: un solo agente con Unity abierto a la vez (candado `Logs/unity.lock`); los subagentes reciben **especificación cerrada y archivos propios**; la sesión principal compila y verifica una vez por tanda.

### 14.5 Orden recomendado si hay poco tiempo

1. Pasos 0, 1 y 2 (laboratorio y entrenamiento cerca del spawn) → ya es una mejora grande.
2. Pasos 3 y 7 (Archivo y libros) → el flujo de aprendizaje se entiende.
3. Pasos 4-6 y 8-9 (campos, caminos, carteles, mapa) → el flujo se *ve*.
4. Pasos 10-12 → late game, destellos, docs.

---

## 16. Rework de la torre (D13)

**Alcance acordado:** rework **visual y de contenido**, misma estructura de pisos y rampas (planta baja biblioteca, 1.er piso estudio con portal y alquimia, 2.º piso, terraza almenada; ver `TowerBuilder`, `MapDetails`, `MapTowerMap`). Modelos nuevos o rehechos con **Opus**.

**Qué cambia el mapa para la torre:** pierde `variants`, `motion` y `shape` (van al Archivo); conserva `elements` y `rare` (terraza). Tiene que sentirse la **«primera etapa»**: el lugar donde el jugador aprende lo básico sin abrumarse.

**Propuesta base (la crítica externa agrega ideas en §12 cuando llegue):**
- **Planta baja = «primera lección» guiada:** el libro `elements` en un atril central bien iluminado, con el papel emisor delante y una **pizarra/cartel de 3 pasos** (leer → dibujar el círculo y un elemento → ver el hechizo). Estantes que ahora tendrían libros de adorno vacíos: reutilizarlos como decorado (libros cerrados, sin interacción) para que no se sientan vacíos.
- **Un «tablón de ruta»** a la entrada con el dibujo del anillo sur y el cartel «Siguiente: Entrenamiento» (usa la misma lógica que el mapa, §9).
- **Terraza = mirador:** desde arriba se ven el anillo y los destellos de color de los 4 campos; es el lugar para entender el valle de un vistazo. Aloja `rare` como premio.
- **Estética Witch Hat Atelier:** madera cálida, velas, plantas, tela violeta; low-poly; reusar materiales para Quest.
- **No tocar:** rampas, portal del estudio ni alquimia salvo lo que haga falta para la estética; sin cambios de controles.

**Ejecución:** como paso propio **13** (después del 9; la documentación del paso 12 va al final de todo), con un subagente **Opus** y especificación cerrada: lista de props nuevos, materiales permitidos y presupuesto de triángulos. Capturas antes/después en `Docs/Capturas/`.

---

## 15. Glosario

- **Archivo:** biblioteca nueva (centro del anillo sur) que reúne los libros de sigilos y signos.
- **Anillo:** los cuatro campos de elemento a ≤ 30 m del Archivo.
- **Campo (de agua, tierra, fuego, viento):** zona de entrenamiento con su libro de hechizos y ejercicios.
- **Sitio (`Site`):** posición/radio/frente de una estructura calculada en `MapStructures.Sites()`.
- **`MapOverrides.json`:** ajustes manuales del mapa que `MapLayout` conserva al regenerar.
- **Late game:** zonas del norte/exterior que piden la base: casitas, atalaya, portales, cueva, santuario.
